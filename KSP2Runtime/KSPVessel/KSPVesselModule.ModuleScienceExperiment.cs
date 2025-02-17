﻿using System.Linq;
using KontrolSystem.KSP.Runtime.KSPScience;
using KontrolSystem.TO2.Binding;
using KSP.Modules;
using KSP.Sim.impl;

namespace KontrolSystem.KSP.Runtime.KSPVessel;

public partial class KSPVesselModule {
    [KSClass("ModuleScienceExperiment")]
    public class ModuleScienceExperimentAdapter(KSPVesselModule.PartAdapter part, Data_ScienceExperiment dataScienceExperiment) : BaseScienceExperimentAdapter<KSPVesselModule.PartAdapter, PartComponent>(part, dataScienceExperiment) {
        [KSField]
        public KSPScienceModule.ExperimentAdapter[] Experiments =>
            dataScienceExperiment.ExperimentStandings.Zip(dataScienceExperiment.Experiments,
                    (standing, config) =>
                        new KSPScienceModule.ExperimentAdapter(part.part.SimulationObject, standing, config))
                .ToArray();
    }
}
