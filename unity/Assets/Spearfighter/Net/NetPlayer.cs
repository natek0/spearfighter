using Fusion;
using UnityEngine;
using Spearfighter.Simulation;
using SN = System.Numerics;

namespace Spearfighter.Net
{
    /// <summary>
    /// A networked player (WS10.1). Fusion-native (D2): the authoritative STATE lives in
    /// [Networked] properties, but the tick LOGIC reuses the exact same engine-agnostic
    /// sim helpers as the offline game (VoxelWorld.MoveBody, SimMath) — so movement +
    /// voxel collision are identical, and Fusion gives us prediction/reconciliation +
    /// interpolation for free. WS10.1 is movement only (no combat/builds yet).
    /// </summary>
    public sealed class NetPlayer : NetworkBehaviour
    {
        [Networked] public Vector3 Feet { get; set; }
        [Networked] public float Yaw { get; set; }
        [Networked] public float Pitch { get; set; }
        [Networked] public float VelY { get; set; }
        [Networked] public NetworkBool Grounded { get; set; }
        [Networked] public int SpawnSlot { get; set; }

        private NetInput _prev;

        public override void Spawned()
        {
            NetArena.Ensure();

            if (HasStateAuthority)
            {
                float z = (SpawnSlot % 2 == 0) ? 8f : -8f;
                Feet = new Vector3(0f, 0f, z);
                Yaw = (SpawnSlot % 2 == 0) ? 0f : Mathf.PI;
                Grounded = true;
            }

            // Local player is first-person: hide our own body so it doesn't fill the view.
            if (HasInputAuthority)
                foreach (var r in GetComponentsInChildren<Renderer>())
                    r.enabled = false;
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out NetInput input)) return;

            var cfg = NetArena.Config;
            float dt = Runner.DeltaTime;

            float yaw = Yaw - input.LookYawDelta;
            float pitch = SimMath.ClampPitch(Pitch - input.LookPitchDelta);

            SN.Vector3 fwd = SimMath.PlanarForward(yaw);
            SN.Vector3 right = SimMath.PlanarRight(yaw);
            SN.Vector3 move = fwd * input.Move.y + right * input.Move.x;
            SN.Vector3 disp = SN.Vector3.Zero;
            if (move.LengthSquared() > 1e-6f)
            {
                move = SimMath.NormalizeSafe(move) * (cfg.MoveSpeed * dt);
                disp.X = move.X; disp.Z = move.Z;
            }

            bool grounded = Grounded;
            float vy = VelY;
            if (input.Jump && !_prev.Jump && grounded) { vy = cfg.JumpSpeed; grounded = false; }
            vy += cfg.Gravity * dt;
            disp.Y = vy * dt;

            SN.Vector3 feet = new SN.Vector3(Feet.x, Feet.y, Feet.z);
            NetArena.World.MoveBody(ref feet, cfg.PlayerRadius, cfg.PlayerHeight, disp,
                out bool g, out bool ceiling, out float _);
            if (g) { vy = 0f; grounded = true; } else grounded = false;
            if (ceiling && vy > 0f) vy = 0f;

            Feet = new Vector3(feet.X, feet.Y, feet.Z);
            Yaw = yaw; Pitch = pitch; VelY = vy; Grounded = grounded;
            _prev = input;
        }

        public override void Render()
        {
            // Fusion interpolates the [Networked] values on proxies; drive the transform.
            transform.position = Feet;
            transform.rotation = Quaternion.Euler(0f, Yaw * Mathf.Rad2Deg + 180f, 0f);

            if (HasInputAuthority)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.transform.position = Feet + Vector3.up * NetArena.Config.EyeHeight;
                    cam.transform.rotation = Quaternion.Euler(-Pitch * Mathf.Rad2Deg, Yaw * Mathf.Rad2Deg + 180f, 0f);
                }
            }
        }
    }
}
