using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using KSP;
using KSP.IO;
using FinePrint;
using Contracts;
using ContractConfigurator;
using ContractConfigurator.CutScene;
using System.Security.Cryptography;

namespace TourismExpanded
{
    public class CheckForSOIChange : ContractBehaviour
    {
        protected Vessel vessel;

        public CheckForSOIChange()
        {
        }
        public CheckForSOIChange(Vessel vessel)
        {
            this.vessel = vessel;
        }
        protected override void OnOffered()
        {
            if (vessel.orbit.nextPatch != null)
            {
                MonoBehaviour.print(vessel.name + " nextPatch is not null");

                contract.Withdraw();
            }
            else
            {
                MonoBehaviour.print(vessel.name + " nextPatch is null");
            }
        }
    }
    public class CheckForSOIChangeFactory : BehaviourFactory
    {
        protected Vessel vessel;
        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<Vessel>(configNode, "vessel", x => vessel = x, this);

            return valid;
        }

        public override ContractBehaviour Generate(ConfiguredContract contract)
        {
            return new CheckForSOIChange(vessel);
        }
    }
    public class SetDeadline : ContractBehaviour
    {
        protected double delay;
        protected double multiplier;
        protected bool multipleDest;
        protected Vessel targetVessel;

        public SetDeadline()
        {
        }
        public SetDeadline(double delay, double multiplier, bool multipleDest, Vessel targetVessel)
        {
            this.delay = delay;
            this.multiplier = multiplier;
            this.multipleDest = multipleDest;
            this.targetVessel = targetVessel;
        }
        protected override void OnOffered()
        {
            if (HighLogic.CurrentGame.Parameters.CustomParams<TourismExpanded.TourismExpandedSettings>().enableDeadlines)
            {
                CalculateDeadline();
            }
        }

        public void CalculateDeadline()
        {
            CelestialBody homePlanet = FlightGlobals.GetHomeBody();
            CelestialBody sun = homePlanet.orbit.referenceBody;
            CelestialBody destPlanet = contract.targetBody;

            if (targetVessel != null)
            {
                double arrivalTime = Planetarium.GetUniversalTime();

                arrivalTime = TravelTime(homePlanet, targetVessel, arrivalTime) + delay;

                arrivalTime = TravelTime(targetVessel, homePlanet, arrivalTime);

                contract.TimeDeadline = (arrivalTime - Planetarium.GetUniversalTime()) * multiplier;
            }
            else if (multipleDest)
            {
                double arrivalTime = Planetarium.GetUniversalTime();

                CelestialBody lastBody = homePlanet;
                HashSet<CelestialBody> bodies = contract.ContractBodies;

                bodies.Remove(homePlanet);
                bodies.Remove(sun);

                foreach (CelestialBody body in bodies.OrderBy(b => (b.referenceBody == sun || b.referenceBody == homePlanet)? b.orbit.semiMajorAxis : b.referenceBody.orbit.semiMajorAxis))
                {
                    arrivalTime = TravelTime(lastBody, body, arrivalTime) + delay;
                    lastBody = body;
                }

                arrivalTime = TravelTime(lastBody, homePlanet, arrivalTime);

                contract.TimeDeadline = (arrivalTime - Planetarium.GetUniversalTime()) * multiplier;
            }
            else if(destPlanet == sun)
            {
                contract.TimeDeadline = TravelTime(sun, homePlanet.orbit.semiMajorAxis, sun.Radius * 2) * 4;//temp fix
            }
            else
            {
                if (!destPlanet.isHomeWorld)
                {
                    double arrivalTime = Planetarium.GetUniversalTime();

                    arrivalTime = TravelTime(homePlanet, destPlanet, arrivalTime) + delay;

                    arrivalTime = TravelTime(destPlanet, homePlanet, arrivalTime);

                    contract.TimeDeadline = (arrivalTime - Planetarium.GetUniversalTime()) * multiplier;
                }
                else
                {
                    contract.TimeDeadline = delay;
                }
            }
        }

        public double TravelTime(CelestialBody origin, CelestialBody dest, double earliestLaunchDate)
        {
            double totalTravelTime = 0;
            double launchTime = 0;

            if (origin.referenceBody == dest.referenceBody)
            {
                launchTime = LambertSolver.NextLaunchWindowUT(origin, dest, earliestLaunchDate);

                Orbit originOrbit = origin.orbit;
                Orbit destOrbit = dest.orbit;

                originOrbit.UpdateFromOrbitAtUT(originOrbit, launchTime, originOrbit.referenceBody);
                destOrbit.UpdateFromOrbitAtUT(destOrbit, launchTime, destOrbit.referenceBody);

                double travelTime = LambertSolver.HohmannTimeOfFlight(originOrbit, destOrbit);
                totalTravelTime = travelTime;

                double minDeltaV = LambertSolver.TransferDeltaV(origin, dest, launchTime, travelTime, origin.scienceValues.spaceAltitudeThreshold + 10000, dest.scienceValues.spaceAltitudeThreshold + 10000);

                for (double i = travelTime / 2; i < travelTime * 2; i += 60 * 60 * 6)
                {
                    double thisDeltaV = LambertSolver.TransferDeltaV(origin, dest, launchTime, i, origin.scienceValues.spaceAltitudeThreshold + 10000, dest.scienceValues.spaceAltitudeThreshold + 10000);

                    if (thisDeltaV < minDeltaV)
                    {
                        minDeltaV = thisDeltaV;
                    }
                }

                for (double i = earliestLaunchDate; i < launchTime; i += 60 * 60 * 6 * 2)
                {
                    double deltaV = minDeltaV * 1.1;

                    for (double j = travelTime / 2; j < travelTime * 2; j += 60 * 60 * 6 * 2)
                    {
                        double thisDeltaV = LambertSolver.TransferDeltaV(origin, dest, i, j, origin.scienceValues.spaceAltitudeThreshold + 10000, dest.scienceValues.spaceAltitudeThreshold + 10000);

                        if (thisDeltaV < minDeltaV * 1.1)
                        {
                            if (i < launchTime)
                            {
                                launchTime = i;
                            }

                            if (thisDeltaV < deltaV)
                            {
                                deltaV = thisDeltaV;
                                totalTravelTime = j;
                            }

                            if (thisDeltaV < minDeltaV)
                            {
                                minDeltaV = thisDeltaV;
                            }
                        }
                    }
                }
            }
            else if(origin == dest.referenceBody)
            {
                totalTravelTime = TravelTime(origin, origin.scienceValues.spaceAltitudeThreshold + 10000, dest.orbit.semiMajorAxis);
                launchTime = earliestLaunchDate;
            }
            else if (origin.referenceBody == dest)
            {
                totalTravelTime = TravelTime(dest, origin.orbit.semiMajorAxis, dest.scienceValues.spaceAltitudeThreshold + 10000);
                launchTime = earliestLaunchDate;
            }
            else if (origin.referenceBody == dest.referenceBody.referenceBody)
            {
                totalTravelTime = TravelTime(origin, dest.referenceBody, earliestLaunchDate);
            }
            else if (origin.referenceBody.referenceBody == dest.referenceBody)
            {
                totalTravelTime = TravelTime(origin.referenceBody, dest, earliestLaunchDate);
            }
            else if (origin.referenceBody.referenceBody == dest.referenceBody.referenceBody)
            {
                totalTravelTime = TravelTime(origin.referenceBody, dest.referenceBody, earliestLaunchDate);
            }
            else
            {
                throw new Exception("Cannot find travel time for " + origin.name + " and " + dest.name);
            }

            MonoBehaviour.print("The travel time between " + origin.name + " and " + dest.name + " = " + KSPUtil.dateTimeFormatter.PrintTimeStamp(totalTravelTime, days: true, years: true) 
                + " Launching at : " +  KSPUtil.dateTimeFormatter.PrintTimeStamp(launchTime, days: true, years: true));

            return totalTravelTime + launchTime;
        }

        public double TravelTime(CelestialBody origin, Vessel dest, double earliestLaunchDate)
        {
            double totalTravelTime = 0;
            double launchTime = 0;

            if (origin == dest.orbit.referenceBody)
            {
                Orbit originOrbit = new Orbit(0, 0, origin.scienceValues.spaceAltitudeThreshold + 10000, 0, 0, 0, 0, origin);
                Orbit destOrbit = dest.orbit;

                launchTime = LambertSolver.NextLaunchWindowUT(originOrbit, destOrbit, earliestLaunchDate);

                originOrbit.UpdateFromOrbitAtUT(originOrbit, launchTime, originOrbit.referenceBody);
                destOrbit.UpdateFromOrbitAtUT(destOrbit, launchTime, destOrbit.referenceBody);

                double travelTime = LambertSolver.HohmannTimeOfFlight(originOrbit, destOrbit);
                totalTravelTime = travelTime;

                double minDeltaV = LambertSolver.TransferDeltaV(originOrbit, destOrbit, launchTime, travelTime);

                for (double i = travelTime / 2; i < travelTime * 2; i += 60 * 60 * 6)
                {
                    double thisDeltaV = LambertSolver.TransferDeltaV(originOrbit, dest.orbit, launchTime, i);

                    if (thisDeltaV < minDeltaV)
                    {
                        minDeltaV = thisDeltaV;
                    }
                }

                for (double i = earliestLaunchDate; i < launchTime; i += 60 * 60 * 6 * 2)
                {
                    double deltaV = minDeltaV * 1.1;

                    for (double j = travelTime / 2; j < travelTime * 2; j += 60 * 60 * 6 * 2)
                    {
                        double thisDeltaV = LambertSolver.TransferDeltaV(originOrbit, destOrbit, i, j);

                        if (thisDeltaV < minDeltaV * 1.1)
                        {
                            if (i < launchTime)
                            {
                                launchTime = i;
                            }

                            if(thisDeltaV < deltaV)
                            {
                                deltaV = thisDeltaV;
                                totalTravelTime = j;
                            }

                            if (thisDeltaV < minDeltaV)
                            {
                                minDeltaV = thisDeltaV;
                            }
                        }
                    }
                }
            }
            else if (origin.referenceBody.referenceBody == dest.orbit.referenceBody)
            {
                launchTime = LambertSolver.NextLaunchWindowUT(origin.orbit, dest.orbit, earliestLaunchDate);

                Orbit originOrbit = origin.orbit;
                Orbit destOrbit = dest.orbit;

                originOrbit.UpdateFromOrbitAtUT(originOrbit, launchTime, originOrbit.referenceBody);
                destOrbit.UpdateFromOrbitAtUT(destOrbit, launchTime, destOrbit.referenceBody);

                double travelTime = LambertSolver.HohmannTimeOfFlight(originOrbit, destOrbit);
                totalTravelTime = travelTime;

                double minDeltaV = LambertSolver.TransferDeltaV(originOrbit, destOrbit, launchTime, travelTime);

                for (double i = travelTime / 2; i < travelTime * 2; i += 60 * 60 * 6)
                {
                    double thisDeltaV = LambertSolver.TransferDeltaV(originOrbit, destOrbit, launchTime, i);

                    if (thisDeltaV < minDeltaV)
                    {
                        minDeltaV = thisDeltaV;
                    }
                }

                for (double i = earliestLaunchDate; i < launchTime; i += 60 * 60 * 6 * 2)
                {
                    double deltaV = minDeltaV * 1.1;

                    for (double j = travelTime / 2; j < travelTime * 2; j += 60 * 60 * 6 * 2)
                    {
                        double thisDeltaV = LambertSolver.TransferDeltaV(originOrbit, destOrbit, i, j);

                        if (thisDeltaV < minDeltaV * 1.1)
                        {
                            if (i < launchTime)
                            {
                                launchTime = i;
                            }

                            if (thisDeltaV < deltaV)
                            {
                                deltaV = thisDeltaV;
                                totalTravelTime = j;
                            }

                            if (thisDeltaV < minDeltaV)
                            {
                                minDeltaV = thisDeltaV;
                            }
                        }
                    }
                }
            }
            else
            {
                totalTravelTime = TravelTime(origin, dest.orbit.referenceBody, earliestLaunchDate);
            }

            MonoBehaviour.print("The travel time between " + origin.name + " and " + dest.name + " = " + KSPUtil.dateTimeFormatter.PrintTimeStamp(totalTravelTime, days: true, years: true)
                + " Launching at : " + KSPUtil.dateTimeFormatter.PrintTimeStamp(launchTime, days: true, years: true));

            return totalTravelTime + launchTime;
        }

        public double TravelTime(Vessel origin, CelestialBody dest, double earliestLaunchDate)
        {
            double totalTravelTime = 0;
            double launchTime = 0;

            if (origin.orbit.referenceBody == dest)
            {
                Orbit originOrbit = origin.orbit;
                Orbit destOrbit = new Orbit(0, 0, dest.scienceValues.spaceAltitudeThreshold + 10000, 0, 0, 0, 0, dest);

                launchTime = LambertSolver.NextLaunchWindowUT(originOrbit, destOrbit, earliestLaunchDate);

                originOrbit.UpdateFromOrbitAtUT(originOrbit, launchTime, origin.orbit.referenceBody);
                destOrbit.UpdateFromOrbitAtUT(destOrbit, launchTime, dest.referenceBody);

                double travelTime = LambertSolver.HohmannTimeOfFlight(originOrbit, destOrbit);
                totalTravelTime = travelTime;

                double minDeltaV = LambertSolver.TransferDeltaV(originOrbit, destOrbit, launchTime, travelTime);

                for (double i = travelTime / 2; i < travelTime * 2; i += 60 * 60 * 6)
                {
                    double thisDeltaV = LambertSolver.TransferDeltaV(originOrbit, destOrbit, launchTime, i);

                    if (thisDeltaV < minDeltaV)
                    {
                        minDeltaV = thisDeltaV;
                    }
                }

                for (double i = earliestLaunchDate; i < launchTime; i += 60 * 60 * 6 * 2)
                {
                    double deltaV = minDeltaV * 1.1;

                    for (double j = travelTime / 2; j < travelTime * 2; j += 60 * 60 * 6 * 2)
                    {
                        double thisDeltaV = LambertSolver.TransferDeltaV(originOrbit, destOrbit, i, j);

                        if (thisDeltaV < minDeltaV * 1.1)
                        {
                            if (i < launchTime)
                            {
                                launchTime = i;
                            }

                            if (thisDeltaV < deltaV)
                            {
                                deltaV = thisDeltaV;
                                totalTravelTime = j;
                            }

                            if (thisDeltaV < minDeltaV)
                            {
                                minDeltaV = thisDeltaV;
                            }
                        }
                    }
                }
            }
            else if (origin.orbit.referenceBody == dest.referenceBody.referenceBody)
            {
                launchTime = LambertSolver.NextLaunchWindowUT(origin.orbit, dest.orbit, earliestLaunchDate);

                Orbit originOrbit = origin.orbit;
                Orbit destOrbit = dest.orbit;

                originOrbit.UpdateFromOrbitAtUT(originOrbit, launchTime, originOrbit.referenceBody);
                destOrbit.UpdateFromOrbitAtUT(destOrbit, launchTime, destOrbit.referenceBody);

                double travelTime = LambertSolver.HohmannTimeOfFlight(originOrbit, destOrbit);
                totalTravelTime = travelTime;

                double minDeltaV = LambertSolver.TransferDeltaV(originOrbit, destOrbit, launchTime, travelTime);

                for (double i = travelTime / 2; i < travelTime * 2; i += 60 * 60 * 6)
                {
                    double thisDeltaV = LambertSolver.TransferDeltaV(originOrbit, destOrbit, launchTime, i);

                    if (thisDeltaV < minDeltaV)
                    {
                        minDeltaV = thisDeltaV;
                    }
                }

                for (double i = earliestLaunchDate; i < launchTime; i += 60 * 60 * 6 * 2)
                {
                    double deltaV = minDeltaV * 1.1;

                    for (double j = travelTime / 2; j < travelTime * 2; j += 60 * 60 * 6 * 2)
                    {
                        double thisDeltaV = LambertSolver.TransferDeltaV(originOrbit, destOrbit, i, j);

                        if (thisDeltaV < minDeltaV * 1.1)
                        {
                            if (i < launchTime)
                            {
                                launchTime = i;
                            }

                            if (thisDeltaV < deltaV)
                            {
                                deltaV = thisDeltaV;
                                totalTravelTime = j;
                            }

                            if (thisDeltaV < minDeltaV)
                            {
                                minDeltaV = thisDeltaV;
                            }
                        }
                    }
                }
            }
            else
            {
                totalTravelTime = TravelTime(origin.orbit.referenceBody, dest, earliestLaunchDate);
            }

            MonoBehaviour.print("The travel time between " + origin.name + " and " + dest.name + " = " + KSPUtil.dateTimeFormatter.PrintTimeStamp(totalTravelTime, days: true, years: true)
                + " Launching at : " + KSPUtil.dateTimeFormatter.PrintTimeStamp(launchTime, days: true, years: true));

            return totalTravelTime + launchTime;
        }

        public double TravelTime(CelestialBody body, double sma1, double sma2)//Assumes coplanar and circular orbits
        {
            double transferSMA = (sma1 + sma2) / 2;

            return Math.PI * Math.Sqrt(Math.Pow(transferSMA, 3) / body.gravParameter);
        }
    }
    public class SetDeadlineFactory : BehaviourFactory
    {
        protected double delay;
        protected double multiplier;
        protected bool multipleDest;
        protected Vessel targetVessel;
        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "delay", x => delay = x * 60 * 60 * 6, this, 10);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "multiplier", x => multiplier = x, this, 1.2);
            valid &= ConfigNodeUtil.ParseValue<bool?>(configNode, "multipleDest", x => multipleDest = x.Value, this, false);
            valid &= ConfigNodeUtil.ParseValue<Vessel>(configNode, "targetVessel", x => targetVessel = x, this, defaultValue: null);

            return valid;
        }

        public override ContractBehaviour Generate(ConfiguredContract contract)
        {
            return new SetDeadline(delay, multiplier, multipleDest, targetVessel);
        }
    }

    public class SetRewardFunds : ContractBehaviour
    {
        protected bool landing;
        protected bool multipleDest;
        protected double multiplier;
        protected int count;
        protected Vessel targetVessel;

        public SetRewardFunds()
        {
        }
        public SetRewardFunds(bool landing, bool multipleDest, double multiplier, int count, Vessel targetVessel)
        {
            this.landing = landing;
            this.multipleDest = multipleDest;
            this.multiplier = multiplier;
            this.count = count;
            this.targetVessel = targetVessel;
        }
        protected override void OnOffered()
        {
            CelestialBody homePlanet = FlightGlobals.GetHomeBody();
            CelestialBody sun = homePlanet.orbit.referenceBody;
            CelestialBody destBody = contract.targetBody;

            double totalDeltaV = EscapeVelocity(homePlanet);

            if(targetVessel != null)
            {
                totalDeltaV += TransferDeltaV(homePlanet, targetVessel) * 2;
            }
            else if (multipleDest)
            {
                CelestialBody lastBody = homePlanet;
                HashSet<CelestialBody> bodies = contract.ContractBodies;

                bodies.Remove(homePlanet);
                bodies.Remove(sun);

                foreach (CelestialBody body in bodies.OrderBy(b => (b.referenceBody == sun || b.referenceBody == homePlanet) ? b.orbit.semiMajorAxis : b.referenceBody.orbit.semiMajorAxis))
                {
                    totalDeltaV += TransferDeltaV(lastBody, body);
                    if (landing && body.hasSolidSurface)
                    {
                        totalDeltaV += EscapeVelocity(body) * 2;
                    }
                    lastBody = body;
                }

                totalDeltaV += TransferDeltaV(lastBody, homePlanet);
            }
            else if (destBody == sun)
            {
                totalDeltaV += TransferDeltaV(sun, homePlanet.orbit.semiMajorAxis, sun.Radius * 2);
            }
            else
            {
                if (!destBody.isHomeWorld)
                {
                    totalDeltaV += TransferDeltaV(homePlanet, destBody);
                    totalDeltaV += TransferDeltaV(destBody, homePlanet);

                    if (landing)
                    {
                        totalDeltaV += EscapeVelocity(contract.targetBody) * 2;
                    }
                }
                else
                {
                }
            }

            MonoBehaviour.print("Total delta v for " + contract.Title + " is " + totalDeltaV);

            double funds = Math.Pow(totalDeltaV, 1.4) * (1 + Math.Log10(count)) * multiplier;

            funds *= HighLogic.CurrentGame.Parameters.CustomParams<TourismExpanded.TourismExpandedSettings>().rewardsFundsModifier;

            contract.FundsAdvance = funds * .25;
            contract.FundsCompletion = funds;
            contract.FundsFailure = funds * .5;
        }

        public double EscapeVelocity(CelestialBody body)
        {
            double result = Math.Sqrt(2 * body.gravParameter / body.Radius);
            MonoBehaviour.print(body.name + "'s escape velocity is " + result);
            return result;
        }

        public double TransferDeltaV(CelestialBody origin, Vessel dest)
        {
            double deltaV = 0;
            double launchTime = 0;

            if (origin.referenceBody == dest.orbit.referenceBody)
            {
                launchTime = LambertSolver.NextLaunchWindowUT(origin.orbit, dest.orbit);

                Orbit originOrbit = origin.orbit;
                Orbit destOrbit = dest.orbit;

                originOrbit.UpdateFromOrbitAtUT(originOrbit, launchTime, origin.referenceBody);
                destOrbit.UpdateFromOrbitAtUT(destOrbit, launchTime, dest.orbit.referenceBody);

                double travelTime = LambertSolver.HohmannTimeOfFlight(originOrbit, destOrbit);

                double minDeltaV = LambertSolver.TransferDeltaV(origin, dest.orbit, launchTime, travelTime, origin.scienceValues.spaceAltitudeThreshold + 10000);
                double minDeltaVTravelTime = travelTime;

                for (double i = travelTime / 2; i < travelTime * 2; i += 60 * 60 * 6)
                {
                    double thisDeltaV = LambertSolver.TransferDeltaV(origin, dest.orbit, launchTime, i, origin.scienceValues.spaceAltitudeThreshold + 10000);

                    if (thisDeltaV < minDeltaV)
                    {
                        minDeltaV = thisDeltaV;
                        minDeltaVTravelTime = i;
                    }
                }

                deltaV = minDeltaV;
            }
            else if (origin.referenceBody.referenceBody == dest.orbit.referenceBody)
            {
                deltaV = TransferDeltaV(origin.referenceBody, dest);
            }
            else
            {
                throw new Exception("Cannot find delta V betweem " + origin.name + " and " + dest.name);
            }

            MonoBehaviour.print("Delta V to transfer between " + origin.name + " and " + dest.name + " = " + deltaV +  " at " + KSPUtil.dateTimeFormatter.PrintTimeStamp(launchTime, days: true, years: true));

            return deltaV;
        }

        public double TransferDeltaV(CelestialBody origin, CelestialBody dest)
        {
            double deltaV = 0;
            double launchTime = 0;

            if (origin.referenceBody == dest.referenceBody)
            {
                launchTime = LambertSolver.NextLaunchWindowUT(origin, dest);

                Orbit originOrbit = origin.orbit;
                Orbit destOrbit = dest.orbit;

                originOrbit.UpdateFromOrbitAtUT(originOrbit, launchTime, origin.referenceBody);
                destOrbit.UpdateFromOrbitAtUT(destOrbit, launchTime, dest.referenceBody);

                double travelTime = LambertSolver.HohmannTimeOfFlight(originOrbit, destOrbit);

                double minDeltaV = LambertSolver.TransferDeltaV(origin, dest, launchTime, travelTime, origin.scienceValues.spaceAltitudeThreshold + 10000, dest.scienceValues.spaceAltitudeThreshold + 10000);
                double minDeltaVTravelTime = travelTime;

                for(double i = travelTime / 2; i < travelTime * 2; i += 60 * 60 * 6)
                {
                    double thisDeltaV = LambertSolver.TransferDeltaV(origin, dest, launchTime, i, origin.scienceValues.spaceAltitudeThreshold + 10000, dest.scienceValues.spaceAltitudeThreshold + 10000);

                    if(thisDeltaV < minDeltaV)
                    {
                        minDeltaV = thisDeltaV;
                        minDeltaVTravelTime = i;
                    }
                }

                deltaV = minDeltaV;
            }
            else if(origin == dest.referenceBody)
            {
                deltaV = TransferDeltaV(origin, origin.scienceValues.spaceAltitudeThreshold + 10000, dest.orbit.semiMajorAxis);
            }
            else if (origin.referenceBody == dest)
            {
                deltaV = TransferDeltaV(dest, origin.orbit.semiMajorAxis, dest.scienceValues.spaceAltitudeThreshold + 10000);
            }
            else if(origin.referenceBody == dest.referenceBody.referenceBody)
            {
                deltaV = TransferDeltaV(origin, dest.referenceBody);
            }
            else if(origin.referenceBody.referenceBody == dest.referenceBody)
            {
                deltaV = TransferDeltaV(origin.referenceBody, dest);
            }
            else if (origin.referenceBody.referenceBody == dest.referenceBody.referenceBody)
            {
                deltaV = TransferDeltaV(origin.referenceBody, dest.referenceBody);
            }
            else
            {
                throw new Exception("Cannot find delta V betweem " + origin.name + " and " + dest.name);
            }

            MonoBehaviour.print("Delta V to transfer between " + origin.name + " and " + dest.name + " = " + deltaV + " at " + KSPUtil.dateTimeFormatter.PrintTimeStamp(launchTime, days: true, years: true));

            return deltaV;
        }

        public double TransferDeltaV(CelestialBody body, double sma1, double sma2)//Assumes coplanar and circular orbits
        {
            double deltaV = 0;

            double originVel = Math.Sqrt(body.gravParameter * ((2 / sma1) - (1 / sma1)));
            double destVel = Math.Sqrt(body.gravParameter * ((2 / sma2) - (1 / sma2)));
            double transferSMA = (sma1 + sma2) / 2;
            double transferPerVel = Math.Sqrt(body.gravParameter * ((2 / sma1) - (1 / transferSMA)));
            double transferApoVel = Math.Sqrt(body.gravParameter * ((2 / sma2) - (1 / transferSMA)));

            deltaV += Math.Abs(originVel - transferPerVel);
            deltaV += Math.Abs(destVel - transferApoVel);

            return deltaV;
        }
    }
    public class SetRewardFundsFactory : BehaviourFactory
    {
        protected bool landing;
        protected bool multipleDest;
        protected double multiplier;
        protected int count;
        protected Vessel targetVessel;
        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<bool?>(configNode, "landing", x => landing = x.Value, this, false);
            valid &= ConfigNodeUtil.ParseValue<bool?>(configNode, "multipleDest", x => multipleDest = x.Value, this, false);
            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "multiplier", x => multiplier = x, this, 1.2);
            valid &= ConfigNodeUtil.ParseValue<int>(configNode, "count", x => count = x, this, 2);
            valid &= ConfigNodeUtil.ParseValue<Vessel>(configNode, "targetVessel", x => targetVessel = x, this, defaultValue: null);

            return valid;
        }

        public override ContractBehaviour Generate(ConfiguredContract contract)
        {
            return new SetRewardFunds(landing, multipleDest, multiplier, count, targetVessel);
        }
    }
}
