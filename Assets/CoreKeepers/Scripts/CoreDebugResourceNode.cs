using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CoreKeepers
{
    public enum MinedResourceKind : byte { CoreShards, Ore }

    public sealed class CoreDebugResourceNode : NetworkBehaviour
    {
        [SerializeField] private MinedResourceKind resourceKind = MinedResourceKind.CoreShards;
        [SerializeField, Min(0.1f)] private float respawnDelay = 5f;
        private readonly NetworkVariable<int> resources = new(0);
        private readonly Dictionary<ulong, int> miningHits = new();
        private readonly List<Transform> pieces = new();
        private Transform visualRoot;
        private double respawnAt;

        public int Resources => resources.Value;
        public MinedResourceKind ResourceKind => resourceKind;

        private void Awake() => CachePieces();

        public override void OnNetworkSpawn()
        {
            CachePieces();
            resources.OnValueChanged += OnResourcesChanged;
            if (IsServer)
                resources.Value = pieces.Count;
            ApplyCurrentPieceState();
        }

        public override void OnNetworkDespawn()
        {
            resources.OnValueChanged -= OnResourcesChanged;
            RestoreAllPieces(false);
        }

        private void Update()
        {
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(resources.Value > 0);
            if (IsServer && resources.Value <= 0 && NetworkManager.ServerTime.Time >= respawnAt)
            {
                miningHits.Clear();
                resources.Value = pieces.Count;
            }
        }

        public int TryMine(ulong minerClientId, bool isBuilder, NetworkObjectReference collectorReference)
        {
            if (!IsServer || resources.Value <= 0 || pieces.Count == 0)
                return 0;

            if (!isBuilder)
            {
                miningHits.TryGetValue(minerClientId, out var hits);
                hits++;
                if (hits < 2)
                {
                    miningHits[minerClientId] = hits;
                    return 0;
                }
            }

            miningHits[minerClientId] = 0;
            var extractedIndex = Mathf.Clamp(pieces.Count - resources.Value, 0, pieces.Count - 1);
            DetachPieceRpc(GetPieceNumber(pieces[extractedIndex]), collectorReference);
            resources.Value--;
            if (resources.Value == 0)
            {
                respawnAt = NetworkManager.ServerTime.Time + respawnDelay;
                miningHits.Clear();
            }
            return 1;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void DetachPieceRpc(int pieceNumber, NetworkObjectReference collectorReference)
        {
            var piece = FindPiece(pieceNumber);
            if (piece == null || !collectorReference.TryGet(out var collector))
                return;
            piece.gameObject.SetActive(true);
            MinedResourceFlyEffect.Animate(piece, collector.transform);
        }

        private void CachePieces()
        {
            pieces.Clear();
            visualRoot = null;
            var prefix = resourceKind == MinedResourceKind.Ore ? "Ore_" : "CoreShard_";
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform)
                    continue;
                if (child.parent == transform && child.name.Contains("Visual"))
                    visualRoot = child;
                if (child.name.StartsWith(prefix) && GetPieceNumber(child) > 0)
                    pieces.Add(child);
            }
            pieces.Sort((left, right) => GetPieceNumber(right).CompareTo(GetPieceNumber(left)));
        }

        private Transform FindPiece(int number)
        {
            foreach (var piece in pieces)
                if (GetPieceNumber(piece) == number)
                    return piece;
            return null;
        }

        private static int GetPieceNumber(Transform piece)
        {
            var separator = piece.name.LastIndexOf('_');
            return separator >= 0 && int.TryParse(piece.name.Substring(separator + 1), out var number) ? number : -1;
        }

        private void OnResourcesChanged(int previous, int current)
        {
            if (previous <= 0 && current > 0)
                RestoreAllPieces(true);
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(current > 0);
        }

        private void ApplyCurrentPieceState()
        {
            var extractedCount = Mathf.Clamp(pieces.Count - resources.Value, 0, pieces.Count);
            for (var index = 0; index < pieces.Count; index++)
                pieces[index].gameObject.SetActive(index >= extractedCount);
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(resources.Value > 0);
        }

        private void RestoreAllPieces(bool active)
        {
            foreach (var piece in pieces)
            {
                var effect = piece != null ? piece.GetComponent<MinedResourceFlyEffect>() : null;
                if (effect != null)
                    effect.RestoreImmediately(active);
                else if (piece != null)
                    piece.gameObject.SetActive(active);
            }
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(active);
        }
    }

    public sealed class MinedResourceFlyEffect : MonoBehaviour
    {
        private const float FlightDuration = 0.72f;
        private const float ArcHeight = 1.35f;
        private Transform collector;
        private Transform originalParent;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 originalLocalScale;
        private Vector3 flightScale;
        private Vector3 startPosition;
        private Vector3 lastTargetPosition;
        private float elapsed;

        public static void Animate(Transform piece, Transform collector)
        {
            var existing = piece.GetComponent<MinedResourceFlyEffect>();
            if (existing != null)
                existing.RestoreImmediately(true);
            var effect = piece.gameObject.AddComponent<MinedResourceFlyEffect>();
            effect.Initialize(piece, collector);
        }

        private void Initialize(Transform piece, Transform target)
        {
            collector = target;
            originalParent = piece.parent;
            originalLocalPosition = piece.localPosition;
            originalLocalRotation = piece.localRotation;
            originalLocalScale = piece.localScale;
            piece.SetParent(null, true);
            startPosition = piece.position;
            flightScale = piece.localScale;
            lastTargetPosition = target != null ? target.position + Vector3.up * 0.9f : startPosition;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / FlightDuration);
            if (collector != null)
                lastTargetPosition = collector.position + Vector3.up * 0.9f;
            var position = Vector3.Lerp(startPosition, lastTargetPosition, t);
            position.y += 4f * ArcHeight * t * (1f - t);
            transform.position = position;
            var pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
            var collectScale = t < 0.78f ? 1f : Mathf.InverseLerp(1f, 0.78f, t);
            transform.localScale = flightScale * (pulse * collectScale);
            if (t >= 1f)
                RestoreImmediately(false);
        }

        public void RestoreImmediately(bool active)
        {
            if (originalParent == null)
            {
                Destroy(gameObject);
                return;
            }
            transform.SetParent(originalParent, false);
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
            transform.localScale = originalLocalScale;
            gameObject.SetActive(active);
            Destroy(this);
        }
    }
}
