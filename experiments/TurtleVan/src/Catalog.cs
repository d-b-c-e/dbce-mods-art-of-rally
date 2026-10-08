using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace TurtleVan
{
    internal static class Catalog
    {
        private sealed class Registration
        {
            internal VehiclePackage Package;
            internal Car MenuCar, Donor;
            internal int Slot, DonorIndex;
        }
        private sealed class Choice { internal Registration Item; }
        private static readonly List<Registration> entries = new List<Registration>();
        private static readonly Dictionary<string, Registration> remembered = new Dictionary<string, Registration>();
        private static readonly ConditionalWeakTable<Season, Choice> selections = new ConditionalWeakTable<Season, Choice>();
        private static bool discovered;
        internal static bool Registered => entries.Count > 0;
        internal static VehiclePackage Selected
        {
            get
            {
                if (!Registered) return null;
                var season = GameModeManager.GetSeasonDataCurrentGameMode();
                if (season == null || !selections.TryGetValue(season, out var choice)) return null;
                var item = choice.Item;
                return item != null && season.CarClass == item.Donor.carClass && season.SelectedCar?.prefabName == item.Donor.prefabName ? item.Package : null;
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
            var cars = CarManager.GetCurrentCarsListForClass(donor.carClass);
            var displays = Displays(chooser, donor.carClass);
            var item = entries.FirstOrDefault(e => e.Package == package);
            int donorIndex = cars.IndexOf(donor);
            int slot = item != null && cars.Contains(item.MenuCar) ? cars.IndexOf(item.MenuCar) : cars.Count;
            if (displays.Count != slot) throw new InvalidOperationException("Unexpected car preview count; refusing to replace a stock slot.");
            var original = displays[donorIndex].CarGameObject;
            GameObject preview = null;
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
                        MenuCar = new Car(package.Manifest.name, donor.carNameIndex, donor.prefabName, donor.livery, donor.carClass, donor.carStats, donor.DLC) };
                    entries.Add(item);
                }
                item.Slot = slot; item.DonorIndex = donorIndex;
                if (!cars.Contains(item.MenuCar)) cars.Add(item.MenuCar);
                displays.Add(display); chooser.AllCars.Add(display);
                Main.Log("Vehicle " + id + " menu preview registered: " + donor.carClass + " index " + slot + "; saved donor index " + donorIndex + ".");
            }
            catch
            {
                if (preview != null) UnityEngine.Object.Destroy(preview);
                throw;
            }
        }

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
                foreach (var item in entries) CarManager.GetCurrentCarsListForClass(item.Donor.carClass).Remove(item.MenuCar);
            }
            [HarmonyPostfix] private static void After(CarChooserManager __instance)
            {
                AddPreviews(__instance); discovered = true;
            }
        }
        private static Registration At(Car.CarClass carClass, int index) => entries.FirstOrDefault(e => e.Donor.carClass == carClass && e.Slot == index);
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
                if (item != null && Displays(__instance, item.Donor.carClass).Count > item.Slot)
                    __instance.SelectCarInClass(item.Donor.carClass, item.Slot);
            }
        }
        [HarmonyPatch(typeof(CarManager), "SetChosenCar", new[] { typeof(int) })]
        private static class ChoosePatch
        {
            [HarmonyPrefix] private static void Before(ref int index)
            {
                if (!Registered) return;
                var season = GameModeManager.GetSeasonDataCurrentGameMode();
                var item = At(season.CarClass, index);
                selections.GetValue(season, s => new Choice()).Item = item;
                if (item == null) return;
                index = new SelectionState(item.Slot, item.DonorIndex).Choose(index, true);
                Main.Log("Selected " + item.Package.Manifest.id + "; native season retains " + item.Donor.prefabName + ".");
            }
        }
        [HarmonyPatch(typeof(CarManager), "SetChosenCar", new[] { typeof(Car) })]
        private static class ChooseObjectPatch
        {
            [HarmonyPrefix] private static void Before(ref Car car)
            {
                if (!Registered) return;
                var value = car;
                var item = entries.FirstOrDefault(e => ReferenceEquals(e.MenuCar, value));
                selections.GetValue(GameModeManager.GetSeasonDataCurrentGameMode(), s => new Choice()).Item = item;
                if (item != null) car = item.Donor;
            }
        }
        [HarmonyPatch(typeof(SaveGame), "SetInt")]
        private static class SavePatch
        {
            [HarmonyPrefix] private static void Before(string key, ref int value)
            {
                if (!Registered || !Enum.TryParse<Car.CarClass>(key, out var carClass)) return;
                var item = At(carClass, value);
                remembered[key] = item;
                if (item != null) value = new SelectionState(item.Slot, item.DonorIndex).ToSavedIndex(value);
            }
        }
        [HarmonyPatch(typeof(SaveGame), "GetInt")]
        private static class LoadPatch
        {
            [HarmonyPostfix] private static void After(string key, ref int __result)
            {
                if (remembered.TryGetValue(key, out var item) && item != null && __result == item.DonorIndex) __result = item.Slot;
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
