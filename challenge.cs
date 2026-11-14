using System;
using System.Collections.Generic;

namespace FitTrack
{
    public class MatchNode
    {
        public string Name;
        public int Score;
        public MatchNode Left, Right;
        public bool IsLeaf { get { return Left == null && Right == null; } }
    }

    public class Challenge
    {
        // HashSet: pigilan ang dobleng kalahok
        private HashSet<string> joined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private List<MatchNode> players = new List<MatchNode>();
        private List<List<MatchNode>> rounds = new List<List<MatchNode>>();

        public MatchNode Root { get; private set; }
        public List<MatchNode> Players { get { return players; } }
        public string Champion { get { return Root == null ? null : Root.Name; } }

        public bool AddPlayer(string name, int score)
        {
            name = name.Trim();
            if (name.Length == 0 || !joined.Add(name)) return false;
            players.Add(new MatchNode { Name = name, Score = score });
            return true;
        }

        public void Reset()
        {
            joined.Clear();
            players.Clear();
            rounds.Clear();
            Root = null;
        }

        // Bottom-up na pagbuo ng tournament tree
        public bool Build()
        {
            if (players.Count < 2) return false;

            rounds.Clear();
            List<MatchNode> current = new List<MatchNode>(players);

            while (current.Count > 1)
            {
                List<MatchNode> next = new List<MatchNode>();
                List<MatchNode> matches = new List<MatchNode>();

                for (int i = 0; i < current.Count; i += 2)
                {
                    if (i + 1 == current.Count)      // odd: bye, aakyat nang walang laban
                    {
                        next.Add(current[i]);
                        break;
                    }

                    MatchNode a = current[i];
                    MatchNode b = current[i + 1];
                    MatchNode winner = (a.Score >= b.Score) ? a : b;   // tabla: naunang sumali

                    MatchNode parent = new MatchNode
                    {
                        Name = winner.Name,
                        Score = winner.Score,
                        Left = a,
                        Right = b
                    };
                    matches.Add(parent);
                    next.Add(parent);
                }

                rounds.Add(matches);
                current = next;
            }

            Root = current[0];
            return true;
        }

        public List<string> DescribeBracket()
        {
            List<string> lines = new List<string>();
            for (int r = 0; r < rounds.Count; r++)
            {
                lines.Add(r == rounds.Count - 1 ? "=== FINAL ===" : "=== Round " + (r + 1) + " ===");
                foreach (MatchNode m in rounds[r])
                    lines.Add(m.Left.Name + " (" + m.Left.Score.ToString("N0") + ")  vs  " +
                              m.Right.Name + " (" + m.Right.Score.ToString("N0") + ")  ->  " + m.Name);
                lines.Add("");
            }
            lines.Add("CHAMPION: " + Champion);
            return lines;
        }
    }
}