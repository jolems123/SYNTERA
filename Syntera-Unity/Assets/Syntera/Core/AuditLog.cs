using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Syntera.Core
{
    public sealed class AuditEvent
    {
        public int Index { get; private set; }
        public string Kind { get; private set; }
        public string StepId { get; private set; }
        public string Detail { get; private set; }
        public string PreviousHash { get; private set; }
        public string Hash { get; private set; }

        internal AuditEvent(int index, string kind, string stepId, string detail, string previousHash)
        {
            Index = index;
            Kind = kind;
            StepId = stepId ?? string.Empty;
            Detail = detail ?? string.Empty;
            PreviousHash = previousHash;
            Hash = Compute(index, Kind, StepId, Detail, previousHash);
        }

        internal static string Compute(int index, string kind, string stepId, string detail, string previousHash)
        {
            string payload = index.ToString(CultureInfo.InvariantCulture) + "\n" + kind + "\n" + stepId + "\n" + detail + "\n" + previousHash;
            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var sb = new StringBuilder(digest.Length * 2);
                foreach (byte b in digest) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }
    }

    // Append-only, hash-linked record of every committed transaction (screenplay section 10, step 5).
    public sealed class AuditLog
    {
        public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";
        private readonly List<AuditEvent> events = new List<AuditEvent>();

        public IList<AuditEvent> Events { get { return events.AsReadOnly(); } }

        public string HeadHash { get { return events.Count == 0 ? GenesisHash : events[events.Count - 1].Hash; } }

        public AuditEvent Append(string kind, string stepId, string detail)
        {
            var e = new AuditEvent(events.Count, kind, stepId, detail, HeadHash);
            events.Add(e);
            return e;
        }

        // Rebuilds a log from persisted fields; Verify() then proves nothing was edited.
        public static AuditLog Restore(IEnumerable<string[]> rows)
        {
            var log = new AuditLog();
            foreach (var r in rows)
            {
                if (r.Length != 3) throw new FormatException("Audit row must be kind, step, detail");
                log.Append(r[0], r[1], r[2]);
            }
            return log;
        }

        public bool Verify()
        {
            string prev = GenesisHash;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Index != i || e.PreviousHash != prev) return false;
                if (AuditEvent.Compute(e.Index, e.Kind, e.StepId, e.Detail, e.PreviousHash) != e.Hash) return false;
                prev = e.Hash;
            }
            return true;
        }
    }
}
