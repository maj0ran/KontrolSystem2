﻿using KontrolSystem.TO2.Binding;
using KSP.Game.Science;

namespace KontrolSystem.KSP.Runtime.KSPScience;

public partial class KSPScienceModule {
    [KSClass("ResearchLocation",
        Description = "Represents a research location of a science experiment.")]
    public class ResearchLocationAdapter {
        private readonly ResearchLocation researchLocation;

        public ResearchLocationAdapter(ResearchLocation researchLocation) {
            this.researchLocation = researchLocation;
        }

        [KSField] public string Id => researchLocation.ResearchLocationId;

        [KSField] public bool RequiresRegion => researchLocation.RequiresRegion;

        [KSField] public string BodyName => researchLocation.BodyName;

        [KSField] public string ScienceRegion => researchLocation.ScienceRegion;

        [KSField] public ScienceSitutation ScienceSituation => researchLocation.ScienceSituation;
    }
}
