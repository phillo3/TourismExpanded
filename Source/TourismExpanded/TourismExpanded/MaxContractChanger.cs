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
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class MaxContractsChanger : MonoBehaviour
    {
        private List<Contract> offeredContracts = new List<Contract>();
        public void Start()
        {
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                offeredContracts.Clear();

                SettingsChanged();

                GameEvents.OnGameSettingsApplied.Add(SettingsChanged);

                GameEvents.Contract.onAccepted.Add(ContractAccepted);
                GameEvents.Contract.onDeclined.Add(ContractDeclined);
                GameEvents.Contract.onFailed.Add(ContractFailed);
                GameEvents.Contract.onOffered.Add(ContractOffered);
            }
        }

        public void OnDestroy()
        {
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                GameEvents.OnGameSettingsApplied.Remove(SettingsChanged);

                GameEvents.Contract.onAccepted.Remove(ContractAccepted);
                GameEvents.Contract.onDeclined.Remove(ContractDeclined);
                GameEvents.Contract.onFailed.Remove(ContractFailed);
                GameEvents.Contract.onOffered.Remove(ContractOffered);
            }
        }

        private void SettingsChanged()
        {
            ContractGroup tourismExpanded;
            tourismExpanded = ContractGroup.AllGroups.Where(cg => cg.name == "TourismExpanded").FirstOrDefault();

            int newMax = HighLogic.CurrentGame.Parameters.CustomParams<TourismExpanded.TourismExpandedSettings>().maxContracts;

            MonoBehaviour.print("Updating max contracts from " + tourismExpanded.maxSimultaneous + " to " + newMax);

            if (tourismExpanded.maxSimultaneous > newMax)
            {
                DeleteOffered();
            }

            tourismExpanded.maxSimultaneous = newMax;
        }

        private void DeleteOffered()
        {
            MonoBehaviour.print("Deleting offered contracts. There are " + offeredContracts.Count + " contracts");

            foreach (Contract c in offeredContracts)
            {
                MonoBehaviour.print(c.Title);
                if (c.Agent.Name == "Far Out Tourism" && c.ContractState == Contract.State.Offered)
                {
                    MonoBehaviour.print("Removing contract " + c.Title);
                    c.Withdraw();
                }
            }
        }

        private void ContractAccepted(Contract c)
        {
            if(offeredContracts.Contains(c))
            {
                offeredContracts.Remove(c);
            }
        }

        private void ContractDeclined(Contract c)
        {
            if (offeredContracts.Contains(c))
            {
                offeredContracts.Remove(c);
            }
        }

        private void ContractFailed(Contract c)
        {
            if (offeredContracts.Contains(c))
            {
                offeredContracts.Remove(c);
            }
        }

        private void ContractOffered(Contract c)
        {
            if (c != null)
            {
                offeredContracts.Add(c);
                MonoBehaviour.print("Adding contract to list: " + c.Title);
            }
        }
    }
}