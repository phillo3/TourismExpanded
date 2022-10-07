using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using KSP;
using KSP.IO;
using Contracts;
using ContractConfigurator;

namespace TourismExpanded
{
    public class TourismExpandedSettings : GameParameters.CustomParameterNode
    {
        //Thanks to https://forum.kerbalspaceprogram.com/index.php?/topic/147576-modders-notes-for-ksp-12/#comment-2754813

        public override string Title { get { return "Tourism Expanded Options"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.CAREER; } }
        public override string Section { get { return "Tourism Expanded"; } }
        public override string DisplaySection { get { return "Tourism Expanded"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return true; } }

        [GameParameters.CustomParameterUI("Enable deadlines", toolTip = "Will not alter existing contracts")]
        public bool enableDeadlines = true;

        [GameParameters.CustomFloatParameterUI("Reward funds modifier", minValue = 0.0f, maxValue = 5.0f, displayFormat = "0.0", toolTip = "Will not alter existing contracts")]
        public double rewardsFundsModifier = 1.0f;

        [GameParameters.CustomIntParameterUI("Max contracts", minValue = 0, maxValue = 100, toolTip = "Lowering this will cancel active contracts")]
        public int maxContracts = 8;

        public override bool Enabled(MemberInfo member, GameParameters parameters)
        {
            return true;
        }

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            return HighLogic.CurrentGame.startScene == GameScenes.SPACECENTER;
        }

        public override IList ValidValues(MemberInfo member)
        {
            return null;
        }
    }
}