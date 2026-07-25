#nullable enable

using System;
using System.Globalization;
using System.Text;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public sealed class LastBearingRoadDeskProjection
    {
        internal LastBearingRoadDeskProjection(
            string leg,
            string route,
            float routeProgressPercent,
            string scout,
            LastBearingRoadDamageBand scoutCondition,
            string cargo,
            int cargoMassKilograms,
            string edge,
            LastBearingRoadSafeLineState edgeState,
            string nextVerb,
            string controls)
        {
            Leg = leg;
            Route = route;
            RouteProgressPercent = routeProgressPercent;
            Scout = scout;
            ScoutCondition = scoutCondition;
            Cargo = cargo;
            CargoMassKilograms = cargoMassKilograms;
            Edge = edge;
            EdgeState = edgeState;
            NextVerb = nextVerb;
            Controls = controls;
        }

        public string Leg { get; }

        public string Route { get; }

        public float RouteProgressPercent { get; }

        public string Scout { get; }

        public LastBearingRoadDamageBand ScoutCondition { get; }

        public string Cargo { get; }

        public int CargoMassKilograms { get; }

        public string Edge { get; }

        public LastBearingRoadSafeLineState EdgeState { get; }

        public string NextVerb { get; }

        public string Controls { get; }
    }

    /// <summary>
    /// Read-only road-book projection. Canonical state remains the sole source
    /// for journey, cargo, condition, and interaction truth.
    /// </summary>
    public static class LastBearingRoadDeskPresenter
    {
        private const string Controls =
            "W / RT GO · S / LT BRAKE · A/D / LS STEER · " +
            "SPACE / LB HANDBRAKE · E / A WORK · R / Y RECENTER";

        public static LastBearingRoadDeskProjection Present(
            LastBearingReadModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (model.ExpeditionPhase != ExpeditionPhase.Outbound &&
                model.ExpeditionPhase != ExpeditionPhase.Returning)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_ROAD_DESK_REQUIRES_DRIVING_PHASE");
            }

            string leg = model.ExpeditionPhase == ExpeditionPhase.Returning
                ? "HOMEBOUND"
                : "OUTBOUND";
            long target = Math.Max(0, model.RouteTargetTicks);
            long progress = Math.Max(
                0,
                Math.Min(model.RouteProgressTicks, target));
            float progressPercent = target == 0
                ? 0f
                : (float)(100d * progress / target);
            int cargoMass =
                LastBearingModeCoordinator
                    .DerivePresentationCargoMassKilograms(model);
            LastBearingRoadDamageBand condition =
                LastBearingModeCoordinator.DerivePresentationDamageBand(
                    model.VehicleConditionMilli);
            LastBearingRoadSafeLineState edge =
                LastBearingRoadSafeLineView.DeriveState(
                    model.VehicleLateralMilli);

            return new LastBearingRoadDeskProjection(
                leg,
                FormatRoute(model, progress, target),
                progressPercent,
                "SCOUT · " +
                model.VehicleConditionMilli.ToString(
                    CultureInfo.InvariantCulture) +
                " / " +
                LastBearingBalanceV1.StartingVehicleConditionMilli.ToString(
                    CultureInfo.InvariantCulture) +
                " · " +
                condition.ToString().ToUpperInvariant(),
                condition,
                FormatCargo(model, cargoMass),
                cargoMass,
                FormatEdge(edge),
                edge,
                FormatNextVerb(model),
                Controls);
        }

        private static string FormatRoute(
            LastBearingReadModel model,
            long progress,
            long target)
        {
            string route = model.RouteKind switch
            {
                RouteKind.CollapsedShortBranch => "WRECK LINE",
                RouteKind.ExposedLongRoute => "DUST LINE",
                _ => "UNMARKED ROAD",
            };
            return route + " · " +
                   progress.ToString(CultureInfo.InvariantCulture) +
                   " / " +
                   target.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatCargo(
            LastBearingReadModel model,
            int cargoMassKilograms)
        {
            var cargo = new StringBuilder(96);
            bool hasCargo = false;
            if (model.HeavyCargoKind == HeavyCargoKind.PumpRotor &&
                model.HeavyCargoCustody == HeavyCargoCustody.Vehicle)
            {
                AppendCargo(cargo, ref hasCargo, "PUMP ROTOR");
            }

            if (model.FrameRailSalvageCustody ==
                FrameRailSalvageCustody.Vehicle)
            {
                AppendCargo(
                    cargo,
                    ref hasCargo,
                    "FRAME RAILS " +
                    model.FrameRailSalvageCargoUnits.ToString(
                        CultureInfo.InvariantCulture) +
                    "U");
            }

            if (model.RepairCargoCustody == RepairCargoCustody.Vehicle)
            {
                string repair = model.RepairCargoKind switch
                {
                    RepairCargoKind.CeramicBearing => "CERAMIC BEARING",
                    RepairCargoKind.FieldSleeve => "FIELD SLEEVE",
                    _ => "REPAIR CARGO",
                };
                AppendCargo(cargo, ref hasCargo, repair);
            }

            if (model.LiquidCargoCustody == LiquidCargoCustody.Vehicle &&
                model.LiquidCargoKind != LiquidCargoKind.None &&
                model.LiquidCargoQuantityMilli > 0)
            {
                AppendCargo(
                    cargo,
                    ref hasCargo,
                    model.LiquidCargoKind.ToString().ToUpperInvariant() +
                    " " +
                    FormatMilli(model.LiquidCargoQuantityMilli));
            }

            return hasCargo
                ? "LOAD · " +
                  cargoMassKilograms.ToString(
                      CultureInfo.InvariantCulture) +
                  " KG · " +
                  cargo
                : "LOAD · EMPTY";
        }

        private static void AppendCargo(
            StringBuilder cargo,
            ref bool hasCargo,
            string item)
        {
            if (hasCargo)
            {
                cargo.Append(" + ");
            }

            cargo.Append(item);
            hasCargo = true;
        }

        private static string FormatEdge(
            LastBearingRoadSafeLineState edge)
        {
            return edge switch
            {
                LastBearingRoadSafeLineState.LeftRisk =>
                    "LEFT OXIDE EDGE · STEER RIGHT",
                LastBearingRoadSafeLineState.RightRisk =>
                    "RIGHT OXIDE EDGE · STEER LEFT",
                _ => "BONE LINE · HELD",
            };
        }

        private static string FormatNextVerb(LastBearingReadModel model)
        {
            if (model.PauseCause != PauseCause.None)
            {
                return "ROAD CLOCK HELD · P TO RESUME";
            }

            if (model.IsWreckLineModulePointAvailable)
            {
                return model.RouteActionKind == RouteActionKind.DeployWinch
                    ? "E / A · WORK THE WRECK-LINE WINCH"
                    : "E / A · SEAL SCOUT FOR THE DUST LINE";
            }

            if (model.IsWreckLineFrameRailRecoveryAvailable)
            {
                return "E / A · LASH THE FRAME RAILS";
            }

            if (model.IsDepotApproachRecoveryAvailable)
            {
                return "E / A · SEAT THE DEPOT BRIDLE";
            }

            return model.ExpeditionPhase == ExpeditionPhase.Returning
                ? "KEEP SCOUT POINTED HOME"
                : "KEEP SCOUT ON THE BONE ROAD";
        }

        private static string FormatMilli(long value)
        {
            long whole = value / 1000;
            long fraction = Math.Abs(value % 1000);
            return whole.ToString(CultureInfo.InvariantCulture) +
                   "." +
                   fraction.ToString("000", CultureInfo.InvariantCulture);
        }
    }
}
