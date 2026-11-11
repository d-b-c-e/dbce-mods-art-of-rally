using System.Reflection;
using ArtOfSimRally.Mod;
using Host=ArtOfSimRally.Mod.Main;
using Clock=ArtOfSimRally.Mod.Time;
using Keys=ArtOfSimRally.Mod.Input;
using ViewSets=ArtOfSimRally.Mod.Triple.TripleScreen;
using V=ArtOfSimRally.Mod.Vector3;
using Q=ArtOfSimRally.Mod.Quaternion;
using T=ArtOfSimRally.Mod.Transform;

static partial class Program
{
    static readonly string[] PoseFields={"BonnetSide","BonnetHeight","BonnetForward","BonnetPitch","BumperSide","BumperHeight","BumperForward","BumperPitch"};
    static float[] Poses(Settings cfg)=>PoseFields.Select(f=>(float)typeof(Settings).GetField(f).GetValue(cfg)).ToArray();
    static void PoseDeltas(Settings cfg,float[] delta)
    { for(int i=0;i<PoseFields.Length;i++){var f=typeof(Settings).GetField(PoseFields[i]);f.SetValue(cfg,(float)f.GetValue(cfg)+delta[i]);} }
    static void Near(float actual,float expected,string why)=>Check(Math.Abs(actual-expected)<.0001f,why+$" ({actual} != {expected})");
    static void PoseNear(float[] actual,float[] expected,string why)
    {for(int i=0;i<actual.Length;i++)Near(actual[i],expected[i],why+" field"+i);}
    static void VectorNear(V actual,V expected,string why)
    {Near(actual.x,expected.x,why+" x");Near(actual.y,expected.y,why+" y");Near(actual.z,expected.z,why+" z");}
    static T Car(string name,float top=1.6f,float front=2,bool measured=true)
    {
        var root=new T{name=name+"(Clone)"};root.Add(new CarDynamics());
        if(measured)Body(root,top,front);
        var target=new T{name="camera target"};root.Add(target);return target;
    }
    static void Body(T root,float top,float front)
    {root.Add(new MeshCollider{sharedMesh=new Mesh{bounds=new Bounds(new V(-.9f,0,-front),new V(.9f,top,front))}});}
    static void Track(T target,Settings cfg,bool realistic=false,bool bumper=false)
    {ViewSets.Realistic=realistic;CameraMountMemory.Track(target,cfg,bumper);}
    static void Capture(Settings cfg)
    {typeof(CameraMountMemory).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic)?.Invoke(null,new object[]{cfg});}
    static float FieldDefault(Settings cfg,bool bumper,int field)
    {
        var method=typeof(CameraMountMemory).GetMethod("Default",BindingFlags.Static|BindingFlags.NonPublic);
        Check(method!=null,"production calculated Default handler missing");
        return (float)method.Invoke(null,new object[]{cfg,bumper,field});
    }
    static void MountedSwitch()
    {
        FreshCameraFixture();var cfg=Host.Settings;T a=Car("A"),b=Car("B",2.4f,3);
        Track(a,cfg);float start=cfg.BonnetHeight;cfg.BonnetHeight+=.237f;
        // No extra frame/Capture before changing target: exercise real Track.
        Track(b,cfg);Track(a,cfg);
        Near(cfg.BonnetHeight,start+.237f,"last car edit lost at target switch");
    }
    static void MountedLastEdit()
    {
        FreshCameraFixture();var cfg=Host.Settings;var a=Car("last-edit");Track(a,cfg);
        float start=cfg.BonnetHeight;
        Keys.Held.Add(cfg.KeyUp);CameraTuner.Update(BonnetCamera.View.Bonnet);Keys.Release();
        CameraTuner.Flush(shutdown:true);Host.Settings=Saved();cfg=Host.Settings;
        Track(Car("last-edit"),cfg);
        Near(cfg.BonnetHeight,start+cfg.CameraMoveStep,"last nudge missing from real XML offsets");
        Check(cfg.CameraMounts.Single(x=>x.Car=="last-edit").Offsets[1]!=0,"save persisted an absolute field without its offset");
    }
    static void MountedPersistence()
    {
        FreshCameraFixture();var cfg=Host.Settings;T a=Car("per-car A"),b=Car("per-car B",2.4f,3);
        var da=new[]{.17f,.237f,.359f,3.7f,-.19f,.283f,.413f,-2.6f};
        var db=new[]{-.21f,.331f,-.45f,-1.3f,.29f,-.187f,.537f,4.4f};
        var expectedA=new Dictionary<bool,float[]>();var expectedB=new Dictionary<bool,float[]>();
        foreach(bool set in new[]{false,true})
        {
            Track(a,cfg,set);var baseA=Poses(cfg);PoseDeltas(cfg,da);cfg.BonnetFOV+=5;cfg.BumperFOV+=7;
            CameraTuner.MarkDirty();Track(b,cfg,set);var baseB=Poses(cfg);
            Near(cfg.BonnetHeight,2.4f*.58f+.08f,"new car inherited last car pose");
            Near(cfg.BonnetFOV,(set?34:75)+5,"mounted FOV not shared across cars");
            PoseDeltas(cfg,db);cfg.BonnetFOV+=2;cfg.BumperFOV+=3;CameraTuner.MarkDirty();
            expectedA[set]=baseA.Zip(da,(x,y)=>x+y).ToArray();expectedB[set]=baseB.Zip(db,(x,y)=>x+y).ToArray();
            Track(a,cfg,set);PoseNear(Poses(cfg),expectedA[set],"A lost car/set pose");
            Near(cfg.BonnetFOV,(set?34:75)+7,"A did not receive shared FOV edit");
            Near(cfg.BumperFOV,(set?34:80)+10,"bumper FOV sharing or set namespace wrong");
            CameraTuner.Flush(shutdown:true);Host.Settings=Saved();cfg=Host.Settings;
            Track(a,cfg,set);PoseNear(Poses(cfg),expectedA[set],"same-target Settings reload reused stale store");
        }
        foreach(bool set in new[]{false,true})
        {
            Track(b,cfg,set,true);PoseNear(Poses(cfg),expectedB[set],"B XML reload or set isolation failed");
            Track(a,cfg,set);PoseNear(Poses(cfg),expectedA[set],"A XML reload or set isolation failed");
        }
        Track(a,cfg,true);var other=MountValues(cfg,true);CameraMountMemory.Reset(cfg,false);
        Near(cfg.BonnetHeight,1.6f*.58f+.08f,"mounted reset not calculated from current car");
        Near(cfg.BonnetFOV,34,"Realistic reset not calculated from seat");
        Check(MountValues(cfg,true).SequenceEqual(other),"reset touched the other mount");
        Track(b,cfg,true);PoseNear(Poses(cfg),expectedB[true],"A reset erased B pose");Near(cfg.BonnetFOV,34,"reset failed global same-set FOV scope");
        Track(a,cfg,false);PoseNear(Poses(cfg),expectedA[false],"Realistic reset erased Game pose");Near(cfg.BonnetFOV,82,"Realistic reset erased Game FOV");
        // XML bytes and failure/retry use production atomic persistence, not a store double.
        Host.SaveSettings();byte[] before=File.ReadAllBytes(Host.Path);cfg.BonnetForward+=.123f;CameraTuner.MarkDirty();
        using(File.Open(Host.Path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {Check(!Host.SaveSettings(),"locked camera store save falsely succeeded");}
        Check(File.ReadAllBytes(Host.Path).SequenceEqual(before),"failed camera save damaged previous XML");
        Check(Host.SaveSettings(),"camera save retry failed");Host.Settings=Saved();cfg=Host.Settings;
        Track(Car("per-car A"),cfg);Near(cfg.BonnetForward,expectedA[false][2]+.123f,"retry/reload lost newest offset");
        var bytes=File.ReadAllBytes(Host.Path);Check(bytes.Length<3||!bytes.Take(3).SequenceEqual(new byte[]{239,187,191}),"camera save added UTF8 BOM");
        Check(!System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(Host.Path),"(?<!\\r)\\n"),"camera XML lost Windows CRLF format");
    }
    static void CustomIdentity()
    {
        FreshCameraFixture();var cfg=Host.Settings;var target=Car("donor");Track(target,cfg);
        cfg.BonnetHeight+=.2f;CameraTuner.MarkDirty();
        var visual=new T{name=CameraCarIdentity.CustomVisual};visual.Add(new SkinnedMeshRenderer{localBounds=new Bounds(new V(-1,0,-3),new V(1,2,3))});target.parent.Add(visual);
        CameraFixture.CustomId=null;Track(target,cfg);
        CameraFixture.CustomId="body-a";Track(target,cfg);float customStart=cfg.BonnetHeight;cfg.BonnetHeight+=.3f;
        CameraTuner.MarkDirty();CameraFixture.CustomId="body-b";Track(target,cfg);
        Near(cfg.BonnetHeight,customStart,"custom package change reused another package pose");
        cfg.BonnetHeight+=.4f;CameraTuner.MarkDirty();CameraFixture.CustomId="body-a";Track(target,cfg);
        Near(cfg.BonnetHeight,customStart+.3f,"custom package A adjustment not retained");
        Check(cfg.CameraMounts.Any(x=>x.Car=="donor+body-a")&&cfg.CameraMounts.Any(x=>x.Car=="donor+body-b"),"custom package identities not persisted separately");
        Check(!cfg.CameraMounts.Any(x=>x.Car=="donor+custom"),"unknown package identity became a persistent key");
        CameraFixture.CustomId=null;
    }
    static void LateBody()
    {
        FreshCameraFixture();var cfg=Host.Settings;var target=Car("late body",measured:false);Track(target,cfg);
        Near(cfg.BonnetHeight,.95f,"unmeasured car did not start from defaults");cfg.BonnetHeight+=.3f;CameraTuner.MarkDirty();
        Body(target.parent,2.4f,3);Clock.unscaledTime+=1.1f;Track(target,cfg);
        Near(cfg.BonnetHeight,2.4f*.58f+.08f+.3f,"late geometry lost offset or never replaced unmeasured start");
        Track(target,cfg,true);cfg.BonnetFOV+=5;CameraTuner.MarkDirty();ViewSets.RealisticStartDegrees=42.38;
        Track(target,cfg,true);Near(cfg.BonnetFOV,47.4f,"changed seat calculation lost offset or kept stale baseline");
        Near(FieldDefault(cfg,false,4),42.4f,"F6 Default did not follow current seat calculation");ViewSets.RealisticStartDegrees=34;
    }
    static void CalculatedFieldDefaults()
    {
        foreach(bool set in new[]{false,true})foreach(bool bumper in new[]{false,true})
        {
            FreshCameraFixture();var cfg=Host.Settings;var car=Car("field defaults",2.4f,3);Track(car,cfg,set,bumper);
            var baseline=MountValues(cfg,bumper);
            // F6 rows use side/height/forward/pitch/FOV order; the existing
            // MountValues assertion helper uses height/forward/side/pitch/FOV.
            int[] order={2,0,1,3,4};
            for(int field=0;field<5;field++)
            {
                float d=FieldDefault(cfg,bumper,field);Near(d,baseline[order[field]],"field Default differs from calculated baseline");
                string prefix=bumper?"Bumper":"Bonnet";
                string[] suffix={"Side","Height","Forward","Pitch","FOV"};
                var f=typeof(Settings).GetField(prefix+suffix[field]);f.SetValue(cfg,d+2.137f);Capture(cfg);
                var other=MountValues(cfg,!bumper);var before=MountValues(cfg,bumper);
                f.SetValue(cfg,FieldDefault(cfg,bumper,field));Capture(cfg);
                var after=MountValues(cfg,bumper);
                for(int i=0;i<after.Length;i++)Near(after[i],i==order[field]?baseline[i]:before[i],"individual Default changed another field");
                Check(MountValues(cfg,!bumper).SequenceEqual(other),"individual Default changed another mount");
            }
            CameraMountMemory.Reset(cfg,bumper);PoseNear(MountValues(cfg,bumper),baseline,"whole Reset and individual Defaults disagree");
        }
    }
    static void ChasePosePersistence()
    {
        var(rig,cam)=ChaseFixture();var cfg=Host.Settings;rig.target=Car("chase A");
        cam.transform.position=new V(10,20,30);var stock=cam.transform.position;
        ChaseFov.Apply(rig,cfg);ChaseFov.Remove();
        Keys.Held.Add(cfg.KeyUp);ChaseFov.Apply(rig,cfg);Keys.Release();
        VectorNear(cam.transform.position,stock+new V(0,cfg.CameraMoveStep,0),"chase pose key did not adjust production camera");
        ChaseFov.Remove();VectorNear(cam.transform.position,stock,"chase pose undo did not restore stock camera");
        var saved=cfg.ChaseCameraPoses.Single(p=>p.Car=="chase A"&&p.View==0&&!p.Realistic);Near(saved.Height,.02f,"chase stored wrong pose key");
        Check(!ChaseFov.Custom(cfg,0,false),"pose-only adjustment incorrectly marked FOV Custom");
        cfg.ChaseFovOffsets[0]=7;
        rig.target=Car("chase B");ChaseFov.Apply(rig,cfg);VectorNear(cam.transform.position,stock,"chase A pose leaked into B");Near(cam.fieldOfView,87,"chase FOV forked by car");ChaseFov.Remove();
        rig.CurrentCameraAngle=rig.CameraAnglesList[1];ChaseFov.Apply(rig,cfg);ChaseFov.Remove();
        Keys.Held.Add(cfg.KeyForward);ChaseFov.Apply(rig,cfg);Keys.Release();ChaseFov.Remove();
        ViewSets.Realistic=true;ChaseFov.Apply(rig,cfg);VectorNear(cam.transform.position,stock,"Game chase pose leaked into Realistic");ChaseFov.Remove();
        Keys.Held.Add(cfg.KeyRight);ChaseFov.Apply(rig,cfg);Keys.Release();ChaseFov.Remove();
        Host.SaveSettings();Host.Settings=Saved();cfg=Host.Settings;
        ChaseFov.Apply(rig,cfg);VectorNear(cam.transform.position,stock+new V(.02f,0,0),"chase Realistic pose lost in XML reload");ChaseFov.Remove();
        ViewSets.Realistic=false;rig.target=Car("chase A");rig.CurrentCameraAngle=rig.CameraAnglesList[0];ChaseFov.Apply(rig,cfg);
        VectorNear(cam.transform.position,stock+new V(0,.02f,0),"chase A pose not restored across car/set/view/reload");ChaseFov.Remove();
        Keys.Pressed.Add(cfg.KeyReset);ChaseFov.Apply(rig,cfg);Keys.Release();
        VectorNear(cam.transform.position,stock,"chase reset retained pose offset");Near(cam.fieldOfView,80,"chase reset retained shared FOV offset");ChaseFov.Remove();
        Check(cfg.ChaseCameraPoses.Any(p=>p.Car=="chase B"&&p.View==1&&p.Realistic)&&cfg.ChaseCameraPoses.Any(p=>p.Car=="chase B"&&p.View==1&&!p.Realistic),"reset erased another car/view/set");
        Host.SaveSettings();var resetSaved=Saved();
        Check(!resetSaved.ChaseCameraPoses.Any(p=>p.Car=="chase A"&&p.View==0&&!p.Realistic),"reset pose deletion lost in XML");
        // Saved offsets survive foreign rotation ownership and Release removes
        // only this consumer's temporary transform/FOV overlay.
        var pose=cfg.ChaseCameraPoses.Single(p=>p.Car=="chase B"&&p.View==1&&!p.Realistic);pose.Pitch=3;
        rig.target=Car("chase B");rig.CurrentCameraAngle=rig.CameraAnglesList[1];ChaseFov.Apply(rig,cfg);ChaseFov.Remove();
        var apply=typeof(ChaseFov).GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(m=>m.Name=="Apply");
        if(apply.GetParameters().Length==3)apply.Invoke(null,new object[]{rig,cfg,false});else ChaseFov.Apply(rig,cfg);
        VectorNear(cam.transform.position,stock,"foreign Camera Mod ownership still received pose overlay");ChaseFov.Remove();
        ChaseFov.Apply(rig,cfg);typeof(ChaseFov).GetMethod("Release",BindingFlags.Static|BindingFlags.NonPublic)?.Invoke(null,null);
        VectorNear(cam.transform.position,stock,"chase Release left pose in camera");
        Check(Math.Abs(cam.transform.rotation.Value.W-1)<.0001f,"chase Release left rotation in camera");
    }
    static void ChasePoseControls()
    {
        int[] field={1,1,2,2,0,0,3,3};float[] sign={1,-1,1,-1,-1,1,1,-1};
        for(int channel=0;channel<8;channel++)
        {
            var(rig,cam)=ChaseFixture();var cfg=Host.Settings;rig.target=Car("pose controls");rig.target.rotation=Q.Euler(0,90,0);
            cam.transform.position=new V(10,20,30);var stock=cam.transform.position;
            ChaseFov.Apply(rig,cfg);ChaseFov.Remove();
            Keys.Held.Add(CameraKeys.Bindings[channel].Get(cfg));
            ChaseFov.Apply(rig,cfg);Keys.Release();
            var pose=cfg.ChaseCameraPoses.Single();float[] v={pose.Side,pose.Height,pose.Forward,pose.Pitch};
            for(int i=0;i<4;i++)Near(v[i],i==field[channel]?sign[channel]*(channel<6?cfg.CameraMoveStep:cfg.CameraTiltStep):0,"chase keyboard mapping wrong");
            var expected=stock+rig.target.rotation*new V(pose.Side,pose.Height,pose.Forward);
            VectorNear(cam.transform.position,expected,"chase translation not in car axes");
            if(channel==2)Near(cam.transform.position.x-stock.x,.02f,"car-relative forward did not follow ninety-degree yaw");
            ChaseFov.Apply(rig,cfg);VectorNear(cam.transform.position,expected,"repeated Apply compounded pose");
            ChaseFov.Remove();VectorNear(cam.transform.position,stock,"Remove left pose after repeated Apply");
            Check(Math.Abs(cam.transform.rotation.Value.W-1)<.0001f,"Remove left tilt after repeated Apply");
        }
    }
    static void Persistence()
    {MountedSwitch();MountedLastEdit();MountedPersistence();CustomIdentity();LateBody();CalculatedFieldDefaults();ChasePosePersistence();ChasePoseControls();}
}
