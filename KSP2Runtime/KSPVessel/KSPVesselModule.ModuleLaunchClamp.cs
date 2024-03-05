﻿using KontrolSystem.TO2.Binding;
using KSP.Modules;
using KSP.Sim.impl;

namespace KontrolSystem.KSP.Runtime.KSPVessel;

public partial class KSPVesselModule {
    [KSClass("ModuleLaunchClamp")]
    public class ModuleLaunchClampAdapter : BaseLaunchClampAdapter<PartAdapter, PartComponent> {

        public ModuleLaunchClampAdapter(PartAdapter part, Data_GroundLaunchClamp dataLaunchClamp) : base(part, dataLaunchClamp) {
        }

        [KSMethod]
        public bool Release() {
            if (!KSPContext.CurrentContext.Game.SpaceSimulation.TryGetViewObject(part.part.SimulationObject,
                    out var viewObject)) return false;

            if (!viewObject.TryGetComponent<Module_GroundLaunchClamp>(out var moduleLaunchClamp)) return false;

            moduleLaunchClamp.Release();
            return dataLaunchClamp.isReleased;
        }
    }
}
