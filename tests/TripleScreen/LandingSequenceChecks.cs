using System;
using System.Collections.Generic;
using ArtOfSimRally.Mod.Triple;
using Dbce.Wheel.Display;

namespace Dbce.TripleScreen.Tests;
internal static class LandingSequenceChecks
{
    private sealed class Host : RacingWindowLanding.IHost
    {
        public float Now { get; set; }
        public IReadOnlyList<MonitorRect> Monitors { get; set; } = new[] {
            new MonitorRect(-1000,0,1000,700,false), new MonitorRect(0,0,1000,700,true),
            new MonitorRect(1000,0,1000,700,false), new MonitorRect(0,-600,800,600,false) };
        internal bool Available = true, Accept = true;
        internal int Resizes, Places, X, Y;
        internal long Window;
        internal RacingWindowLanding.Observation Sample = new() {
            Window=1, X=0, Y=-600, Width=800, Height=600, EngineWidth=800, EngineHeight=600 };
        public bool Observe(out RacingWindowLanding.Observation observation) { observation=Sample; return Available; }
        public void Resize(int width,int height) { Resizes++; }
        public bool Place(long window,int x,int y) { Places++; Window=window; X=x; Y=y; return Accept; }
        public void Log(string message) { }
        internal void Ready() { Sample.Width=Sample.EngineWidth=1000; Sample.Height=Sample.EngineHeight=700; Sample.Windowed=true; Sample.OffsetX=8; Sample.OffsetY=31; }
    }
    internal static void Run(Action<bool,string> check)
    {
        var h=new Host(); var landing=new RacingWindowLanding(h); landing.Update();
        check(h.Resizes==1 && landing.BlocksOutputResize,"landing exclusively owns resize immediately");
        h.Now=1; landing.Update(); check(h.Places==0,"elapsed delay alone cannot place");
        h.Ready(); h.Sample.Windowed=false; landing.Update(); check(h.Places==0,"old fullscreen mode cannot place");
        h.Sample.Windowed=true; h.Sample.Width=999; landing.Update(); check(h.Places==0,"native client must match engine");
        h.Sample.Width=1000; h.Available=false; landing.Update(); check(landing.BlocksOutputResize,"temporary unavailable retains pending");
        h.Available=true; h.Sample.Window=2; landing.Update();
        check(h.Places==1 && h.Window==2 && h.X==-8 && h.Y==-31,"validated replacement uses client border offsets");
        landing.Update(); check(h.Resizes==1 && h.Places==1 && !landing.BlocksOutputResize,"terminal episode does not repeat");
        h.Monitors=new[] { h.Monitors[3],h.Monitors[2],h.Monitors[1],h.Monitors[0] }; landing.Update();
        check(h.Resizes==1,"enumeration reorder is unchanged topology");
        h=new Host(); landing=new RacingWindowLanding(h); landing.Update(); h.Now=10; h.Ready(); landing.Update(); landing.Update();
        check(h.Places==0 && h.Resizes==1 && !landing.BlocksOutputResize,"deadline terminal even when size arrives late");
        h=new Host(); landing=new RacingWindowLanding(h); landing.Update(); h.Ready(); h.Accept=false; landing.Update(); landing.Update();
        check(h.Places==1 && h.Resizes==1,"native refusal is terminal");
        h=new Host(); landing=new RacingWindowLanding(h); landing.Update();
        h.Monitors=new[] {new MonitorRect(0,0,1000,700,true)}; h.Ready(); landing.Update();
        check(h.Places==0 && !landing.BlocksOutputResize,"topology change cancels stale target");
    }
}
