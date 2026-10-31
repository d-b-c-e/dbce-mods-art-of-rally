using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace RallyCustomVehicles
{
    internal static class Catalog
    {
        private sealed class Registration
        {
            internal VehiclePackage Package;
            internal Car MenuCar, Donor;
            internal int Slot, DonorIndex;
            internal bool Active;
        }
        private sealed class Choice { internal Registration Item; }
        private static readonly List<Registration> entries = new List<Registration>();
        private static readonly Dictionary<string, Registration> remembered = new Dictionary<string, Registration>();
        private static readonly ConditionalWeakTable<Season, Choice> selections = new ConditionalWeakTable<Season, Choice>();
        private static bool discovered;
        internal static bool Registered => entries.Any(e=>e.Active);

        // Free Roam's class picker maps list position to Car.CarClass, so "Custom" is the value after the ten stock
        // classes. It exists only in the menu: starting Free Roam hands the game the donor's class and car, and the
        // Free Roam season is never saved, so no native save or switch ever sees it.
        internal const Car.CarClass CustomClass = Car.CarClass.COUNT;
        private static readonly List<Car> customCars = new List<Car>();
        private static readonly List<CarMenuDisplay> customDisplays = new List<CarMenuDisplay>();
        private static Registration launched;   // the custom car Free Roam last started with; the menu returns to Custom
        private static bool InCustomMenu => Registered && GameModeManager.GameMode == GameModeManager.GAME_MODES.FREEROAM;
        private static Registration At(int slot) => entries.FirstOrDefault(e => e.Active && e.Slot == slot);
        private static List<string> CurrentIds() => entries.Where(e => e.Active).OrderBy(e => e.Slot).Select(e => e.Package.Manifest.id).ToList();
        internal static VehiclePackage Selected
        {
            get
            {
                if (!Registered) return null;
                var season = GameModeManager.GetSeasonDataCurrentGameMode();
                if (season == null || !selections.TryGetValue(season, out var choice)) return null;
                var item = choice.Item;
                return item != null && item.Active && (season.CarClass == item.Donor.carClass || season.CarClass == CustomClass) &&
                    season.SelectedCar?.prefabName == item.Donor.prefabName ? item.Package : null;
            }
        }

        internal static void DiscoverExistingChooser()
        {
            if (discovered) return;
            foreach (var chooser in Resources.FindObjectsOfTypeAll<CarChooserManager>())
            {
                if (!chooser.gameObject.scene.IsValid() || chooser.AllCars == null || chooser.AllCars.Count == 0) continue;
                AddPreviews(chooser); discovered = true;
            }
        }
        private static List<CarMenuDisplay> Displays(CarChooserManager c, Car.CarClass carClass)
        {
            switch (carClass)
            {
                case Car.CarClass.GROUP_2: return c.Sixties_Cars;
                case Car.CarClass.GROUP_3: return c.Seventies_Cars;
                case Car.CarClass.GROUP_4: return c.Eighties_Cars;
                case Car.CarClass.GROUP_B: return c.GroupB_Cars;
                case Car.CarClass.GROUP_S: return c.GroupS_Cars;
                case Car.CarClass.GROUP_A: return c.GroupA_Cars;
                case Car.CarClass.BONUS_VANS: return c.Bonus_Vans;
                case Car.CarClass.BONUS_PIAGGIO: return c.Bonus_Piaggios;
                case Car.CarClass.BONUS_DAKAR: return c.Bonus_Dakars;
                case Car.CarClass.BONUS_LOGGING: return c.Bonus_Logging;
                default: throw new InvalidOperationException("Unsupported vehicle class.");
            }
        }
        private static void AddPreviews(CarChooserManager chooser)
        {
            if (!Main.Enabled) return;
            foreach (var package in Main.Packages)
            {
                try { AddPreview(chooser, package); }
                catch (Exception ex) { Main.Log("Vehicle preview skipped: " + package.Manifest.id); Main.LogError(ex); }
            }
        }
        private static void AddPreview(CarChooserManager chooser, VehiclePackage package)
        {
            string id = package.Manifest.id;
            if (chooser.AllCars.Any(c => c.CarGameObject != null && c.CarGameObject.GetComponent<PreviewOwner>()?.Id == id)) return;
            // AllCarsList stays stock: saves, setup, unlocks and Steam stats use it.
            var donor = CarManager.AllCarsList.FirstOrDefault(c => c.prefabName == package.Manifest.donorPrefab);
            if (donor == null) { Main.Log("Vehicle " + id + " skipped: donor/DLC unavailable: " + package.Manifest.donorPrefab); return; }
            // Custom cars are listed only in the Custom class; the donor's own class stays stock.
            var cars = customCars;
            var displays = customDisplays;
            var item = entries.FirstOrDefault(e => e.Package == package);
            int donorIndex = CarManager.GetCurrentCarsListForClass(donor.carClass).IndexOf(donor);
            int slot = item != null && cars.Contains(item.MenuCar) ? cars.IndexOf(item.MenuCar) : cars.Count;
            if (displays.Count != slot) throw new InvalidOperationException("Unexpected custom preview count.");
            var original = Displays(chooser, donor.carClass)[donorIndex].CarGameObject;
            GameObject preview = null;
            var transaction=new MutationJournal();
            try
            {
                preview = UnityEngine.Object.Instantiate(original, original.transform.parent, false);
                preview.name = id + "_MenuPreview";
                preview.SetActive(false);
                var owner = preview.AddComponent<PreviewOwner>(); owner.Id = id;
                var body = new GameObject("CustomVehicle_Model");
                body.transform.SetParent(preview.transform, false);
                Main.PopulateModel(package, body, original.layer, new Dictionary<string, Transform>(), owner.Resources);
                var donorBounds = LocalBounds(preview.transform, false, body.transform);
                var modelBounds = LocalBounds(body.transform, true, null);
                float scale = donorBounds.size.z / modelBounds.size.z;
                if (scale < .1f || scale > 10f) throw new InvalidOperationException("Unexpected menu model dimensions.");
                body.transform.localScale = Vector3.one * scale;
                body.transform.localPosition = new Vector3(donorBounds.center.x - modelBounds.center.x * scale,
                    donorBounds.min.y - modelBounds.min.y * scale, donorBounds.center.z - modelBounds.center.z * scale);
                foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.transform.IsChildOf(body.transform) || renderer.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) renderer.enabled = false;
                }
                var display = new CarMenuDisplay(preview);
                // Do not let donor livery UI mutate a material shared with stock previews.
                display.CarMaterial = null;
                if (item == null)
                {
                    item = new Registration { Package = package, Donor = donor,
                        MenuCar = new Car(package.Manifest.name, donor.carNameIndex, donor.prefabName, donor.livery, CustomClass, CopyStats(donor.carStats), donor.DLC) };
                    transaction.Change(()=>entries.Add(item),()=>entries.Remove(item));
                }
                var priorDonor=item.Donor; int priorSlot=item.Slot,priorIndex=item.DonorIndex;
                transaction.Change(()=> {item.Donor=donor; item.Slot=slot; item.DonorIndex=donorIndex;},()=> {item.Donor=priorDonor; item.Slot=priorSlot; item.DonorIndex=priorIndex;});
                if (!cars.Contains(item.MenuCar)) transaction.Change(()=>cars.Add(item.MenuCar),()=>cars.Remove(item.MenuCar));
                transaction.Change(()=>displays.Add(display),()=>displays.Remove(display));
                transaction.Change(()=>chooser.AllCars.Add(display),()=>chooser.AllCars.Remove(display));
                transaction.Change(()=>item.Active=true,()=>item.Active=false);
                Main.Log("Vehicle " + id + " menu preview registered: Custom class index " + slot + "; donor " + donor.carClass + " " + donor.prefabName + ".");
            }
            catch
            {
                transaction.Rollback(Main.LogError);
                if (preview != null) UnityEngine.Object.Destroy(preview);
                throw;
            }
        }
        private static Car.CarStats CopyStats(Car.CarStats s) => new Car.CarStats(s.YearUnlocked,s.UnlockedSaveConstant,s.YearBonusUnlocked,s.BonusUnlockedSaveConstant,
            s.Engine,s.Gears,s.Transmission,s.Aspiration,s.Country,s.LoreKey,s.StatsOdometer,s.StatsPodiums,s.StatsEvents) {IsUnlocked=s.IsUnlocked,IsBonusUnlocked=s.IsBonusUnlocked};

        private static Bounds LocalBounds(Transform root, bool includeAll, Transform exclude)
        {
            var result = new Bounds(); bool first = true;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || (exclude != null && filter.transform.IsChildOf(exclude)) ||
                    (!includeAll && filter.name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    p = root.InverseTransformPoint(filter.transform.TransformPoint(p));
                    if (first) { result = new Bounds(p, Vector3.zero); first = false; } else result.Encapsulate(p);
                }
            }
            if (first) throw new InvalidOperationException("Menu model has no mesh bounds.");
            return result;
        }

        // The native initializer assumes authored children for every list entry.
        [HarmonyPatch(typeof(CarChooserManager), "Awake")]
        private static class ChooserPatch
        {
            [HarmonyPrefix] private static void Before()
            {
                foreach (var item in entries) item.Active=false;
                customCars.Clear(); customDisplays.Clear();   // the old chooser's previews go with it
            }
            [HarmonyPostfix] private static void After(CarChooserManager __instance)
            {
                AddPreviews(__instance); discovered = true;
            }
        }
        [HarmonyPatch(typeof(CarChooserManager), "DisplayCarInRallyCompleteScreen")]
        private static class CompletionDisplayPatch
        {
            [HarmonyPostfix] private static void After(CarChooserManager __instance)
            {
                // The native completion screen reads the saved donor's carNameIndex.
                // Correct only this presentation; keep its season/save data native.
                var selected = Selected;
                if (selected == null) return;
                var item = entries.FirstOrDefault(e => e.Package == selected);
                if (item != null && customDisplays.Count > item.Slot)
                    __instance.SelectCarInClass(CustomClass, item.Slot);
            }
        }
        [HarmonyPatch(typeof(CarManager), "SetChosenCar", new[] { typeof(int) })]
        private static class ChoosePatch
        {
            [HarmonyPrefix] private static bool Before(int index)
            {
                if (!Registered) return true;
                var season = GameModeManager.GetSeasonDataCurrentGameMode();
                if (season == null) return true;
                if (season.CarClass != CustomClass) { selections.GetValue(season, s => new Choice()).Item = null; launched = null; return true; }
                var item = At(CustomClassMenu.Clamp(index, customCars.Count));
                if (item == null) { season.CarClass = Car.CarClass.GROUP_2; return true; }   // never leave the season on a class only the menu knows
                selections.GetValue(season, s => new Choice()).Item = item;
                season.SelectedCar = item.Donor;   // native season data always holds the donor
                return false;
            }
        }
        [HarmonyPatch(typeof(CarManager), "SetChosenCar", new[] { typeof(Car) })]
        private static class ChooseObjectPatch
        {
            [HarmonyPrefix] private static void Before(ref Car car)
            {
                if (!Registered) return;
                var value = car;
                var item = entries.FirstOrDefault(e => e.Active && ReferenceEquals(e.MenuCar, value));
                selections.GetValue(GameModeManager.GetSeasonDataCurrentGameMode(), s => new Choice()).Item = item;
                if (item != null) car = item.Donor;
            }
        }
        [HarmonyPatch(typeof(SaveGame), "SetInt")]
        private static class SavePatch
        {
            // The menu stores each class's car index under the class name; Custom's ("COUNT") is remembered by package id.
            [HarmonyPrefix] private static void Before(string key, int value)
            {
                if (!Registered || key != CustomClass.ToString()) return;
                var item = At(value);
                remembered[key] = item;
                try { Main.Selections.Set(key, item?.Package.Manifest.id); } catch(Exception ex) { Main.LogError(ex); }
            }
        }
        [HarmonyPatch(typeof(SaveGame), "GetInt")]
        private static class LoadPatch
        {
            [HarmonyPostfix] private static void After(string key, ref int __result)
            {
                if (key != CustomClass.ToString()) return;
                string id=remembered.TryGetValue(key,out var item) ? item?.Package.Manifest.id : Main.Selections.Get(key);
                __result=CustomClassMenu.SlotFor(id,__result,CurrentIds());
            }
        }

        // Free Roam's class list gains "custom" while custom cars are registered.
        [HarmonyPatch(typeof(CarManager), "GetClassStrings")]
        private static class ClassStringsPatch
        {
            [HarmonyPostfix] private static void After(List<string> __result) { if (InCustomMenu) __result.Add("custom"); }
        }
        [HarmonyPatch(typeof(CarManager), "SetChosenClass", new[] { typeof(int) })]
        private static class ChooseClassPatch
        {
            [HarmonyPrefix] private static bool Before(int index)
            {
                if (index != (int)CustomClass || !InCustomMenu) { launched = null; return true; }
                GameModeManager.GetSeasonDataCurrentGameMode().CarClass = CustomClass;
                return false;
            }
        }
        [HarmonyPatch(typeof(CarManager), "GetCurrentCarsListForClass")]
        private static class CarsListPatch
        {
            [HarmonyPrefix] private static bool Before(Car.CarClass theClass, ref List<Car> __result)
            { if (theClass != CustomClass) return true; __result = customCars; return false; }
        }
        [HarmonyPatch(typeof(CarManager), "GetCurrentCarsForClass")]
        private static class CarNamesPatch
        {
            [HarmonyPrefix] private static bool Before(Car.CarClass theClass, ref List<string> __result)
            { if (theClass != CustomClass) return true; __result = customCars.Select(c => c.name).ToList(); return false; }
        }
        [HarmonyPatch(typeof(CarManager), "GetCurrentPrefabCarsForClass")]
        private static class CarPrefabsPatch
        {
            [HarmonyPrefix] private static bool Before(Car.CarClass theClass, ref List<string> __result)
            { if (theClass != CustomClass) return true; __result = customCars.Select(c => c.prefabName).ToList(); return false; }
        }
        [HarmonyPatch(typeof(GroupTitle), "GetStringFromClass")]
        private static class GroupTitlePatch
        {
            [HarmonyPrefix] private static bool Before(Car.CarClass carClass, ref string __result)
            { if (carClass != CustomClass) return true; __result = "Custom"; return false; }
        }

        // The car chooser's per-class switches throw on an unknown class; the Custom class uses its own previews.
        private static readonly System.Reflection.MethodInfo ShowOne = AccessTools.Method(typeof(CarChooserManager), "ShowOneCarForClass");
        private static readonly System.Reflection.MethodInfo ShowAdditional = AccessTools.Method(typeof(CarChooserManager), "ShowAdditionalCarForClass");
        private static readonly System.Reflection.MethodInfo DeselectAll = AccessTools.Method(typeof(CarChooserManager), "DeselectAllCars");
        [HarmonyPatch(typeof(CarChooserManager), "EnableAllCarsInCurrentClass")]
        private static class EnableClassPatch
        {
            [HarmonyPrefix] private static bool Before()
            {
                if (GameModeManager.GetSeasonDataCurrentGameMode()?.CarClass != CustomClass) return true;
                foreach (var display in customDisplays) display.SetActive(true);
                return false;
            }
        }
        [HarmonyPatch(typeof(CarChooserManager), "SelectCarInClass")]
        private static class SelectPatch
        {
            [HarmonyPrefix] private static bool Before(CarChooserManager __instance, ref Car.CarClass carClass, int index, ref CarMenuDisplay __result)
            {
                if (carClass != CustomClass) return true;
                if (customDisplays.Count == 0) { carClass = Car.CarClass.GROUP_2; return true; }
                __instance.DisableAllCars();
                DeselectAll.Invoke(__instance, null);
                index = CustomClassMenu.Clamp(index, customDisplays.Count);
                customDisplays[index].isSelected = true;
                __result = (CarMenuDisplay)ShowOne.Invoke(__instance, new object[] { customDisplays, index });
                return false;
            }
        }
        [HarmonyPatch(typeof(CarChooserManager), "SelectAdditionalCarInClass")]
        private static class SelectAdditionalPatch
        {
            [HarmonyPrefix] private static bool Before(CarChooserManager __instance, Car.CarClass carClass, int index)
            {
                if (carClass != CustomClass) return true;
                if (index >= 0 && index < customDisplays.Count)
                { customDisplays[index].isSelected = true; ShowAdditional.Invoke(__instance, new object[] { customDisplays, index }); }
                return false;
            }
        }
        [HarmonyPatch(typeof(CarChooserManager), "ChangeCarClass")]
        private static class ChangeClassPatch
        {
            [HarmonyPrefix] private static bool Before(CarChooserManager __instance, int carClass)
            {
                if (carClass != (int)CustomClass) return true;
                __instance.DisableAllCars();
                if (customDisplays.Count > 0) ShowOne.Invoke(__instance, new object[] { customDisplays, 0 });
                return false;
            }
        }
        [HarmonyPatch(typeof(CarChooserManager), "DisplayCarInLocation")]
        private static class LocationPatch
        {
            [HarmonyPrefix] private static bool Before(Car.CarClass carclass) => carclass != CustomClass;
        }

        // Starting Free Roam from the Custom class: the game gets the donor's class and car (its own data, saves and
        // physics path), and the custom choice rides along as the selection. Coming back, the menu shows Custom again.
        [HarmonyPatch(typeof(FreeroamManager), "LoadFreeRoam")]
        private static class StartPatch
        {
            [HarmonyPrefix] private static void Before()
            {
                var season = GameModeManager.GetSeasonDataCurrentGameMode();
                if (season == null || season.CarClass != CustomClass) return;
                Registration item = null;
                if (selections.TryGetValue(season, out var choice)) item = choice.Item;
                item = item != null && item.Active ? item : At(SaveGame.GetInt(CustomClass.ToString()));
                if (item == null) { season.CarClass = Car.CarClass.GROUP_2; season.SelectedCar = CarManager.GetCurrentCarsListForClass(Car.CarClass.GROUP_2)[0]; return; }
                season.CarClass = item.Donor.carClass;
                season.SelectedCar = item.Donor;
                selections.GetValue(season, s => new Choice()).Item = item;
                launched = item;
                Main.Log("Free Roam with " + item.Package.Manifest.id + " (Custom class); native season: " + item.Donor.carClass + " " + item.Donor.prefabName + ".");
            }
        }
        [HarmonyPatch(typeof(CustomButtonCars), "Init")]
        private static class ReturnPatch
        {
            [HarmonyPrefix] private static void Before()
            {
                var season = GameModeManager.GetSeasonDataCurrentGameMode();
                if (launched != null && launched.Active && InCustomMenu && season != null && season.CarClass == launched.Donor.carClass && ReferenceEquals(season.SelectedCar, launched.Donor))
                    season.CarClass = CustomClass;
            }
        }
        [HarmonyPatch]
        private static class StatsPatch
        {
            [HarmonyTargetMethods] private static IEnumerable<System.Reflection.MethodBase> Targets() =>
                typeof(CarManager).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .Where(p => p.Name.StartsWith("ReadOnly", StringComparison.Ordinal) && p.PropertyType == typeof(List<Car>)).Select(p => p.GetGetMethod());
            [HarmonyPostfix] private static void After(ref List<Car> __result)
            {
                // Some native getters return shared lists; always filter into a copy.
                if (Registered) __result = __result.Where(c => !entries.Any(e => ReferenceEquals(e.MenuCar, c))).ToList();
            }
        }
    }

    public sealed class PreviewOwner : MonoBehaviour
    {
        internal string Id;
        internal readonly List<UnityEngine.Object> Resources = new List<UnityEngine.Object>();
        private void OnDestroy()
        {
            foreach (var resource in Resources) if (resource != null) UnityEngine.Object.Destroy(resource);
            Resources.Clear();
        }
    }
}
