using System;

namespace MonsterPouch.Gameplay.Machines
{
    /// <summary>
    /// Immutable catalogue/policy snapshot prepared by MachineDrawEngine.
    /// Every operation still validates the supplied mutable state and ownership.
    /// No internal definition, policy, array, dictionary or delegate is exposed.
    /// </summary>
    public sealed class PreparedMachineContext
    {
        readonly Func<MachineState> createState;
        readonly Func<MachineState, MachineOwnership, MachinePreview> preview;
        readonly Func<MachineState, MachineOwnership, MachineDrawRequest, double, MachineDrawResult> draw;
        public string MachineId { get; }
        public int CatalogVersion { get; }
        public string PolicyId { get; }
        public int PolicyVersion { get; }

        internal PreparedMachineContext(string machineId, int catalogVersion, string policyId, int policyVersion,
            Func<MachineState> createState,
            Func<MachineState, MachineOwnership, MachinePreview> preview,
            Func<MachineState, MachineOwnership, MachineDrawRequest, double, MachineDrawResult> draw)
        {
            MachineId = machineId; CatalogVersion = catalogVersion; PolicyId = policyId; PolicyVersion = policyVersion;
            this.createState = createState; this.preview = preview; this.draw = draw;
        }
        public MachineState CreateState() => createState();
        public MachinePreview Preview(MachineState state, MachineOwnership ownership) => preview(state, ownership);
        public MachineDrawResult TryDraw(MachineState state, MachineOwnership ownership, MachineDrawRequest request, double sample01)
            => draw(state, ownership, request, sample01);
    }
}
