﻿using KontrolSystem.TO2.Binding;
using KSP.Game.Science;
using KSP.Sim.impl;

namespace KontrolSystem.KSP.Runtime.KSPScience;

public partial class KSPScienceModule {
    [KSClass("ResearchReport",
        Description = "Represents the stored report of a science experiment")]
    public class ResearchReportAdapter {
        private readonly ResearchReport researchReport;
        private readonly ScienceStorageComponent scienceStorageComponent;

        public ResearchReportAdapter(ScienceStorageComponent scienceStorageComponent, ResearchReport researchReport) {
            this.scienceStorageComponent = scienceStorageComponent;
            this.researchReport = researchReport;
        }

        [KSField(Description = "Get the research location the experiment was performed at.")]
        public ResearchLocationAdapter ResearchLocation => new(researchReport.Location);

        [KSField(Description = "Get the definition of the experiment.")]
        public ExperimentDefinitionAdapter Definition => new(
            KSPContext.CurrentContext.Game.ScienceManager.ScienceExperimentsDataStore.GetExperimentDefinition(
                researchReport.ExperimentID)
        );

        [KSField] public string ResearchLocationId => researchReport.ResearchLocationID;

        [KSField] public string ExperimentId => researchReport.ExperimentID;

        [KSField] public ScienceReportType ReportType => researchReport.ResearchReportType;

        [KSField] public bool TransmissionStatus => researchReport.TransmissionStatus;

        [KSField] public double TransmissionPercentage => researchReport.TransmissionPercentage;

        [KSField] public double EcRequired => researchReport.EcRequired;

        [KSField] public double TimeRequired => researchReport.TimeRequired;

        [KSField] public double TransmissionSize => researchReport.TransmissionSize;

        [KSMethod]
        public bool StartTransmit() {
            return scienceStorageComponent.StartReportTransmission(researchReport.ResearchReportKey);
        }
    }
}
