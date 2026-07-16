using System;
using System.Collections.Generic;
using System.Text;
using SimulateurPliage.Materiel;

namespace SimulateurPliage.Pliage
{
    /// <summary>Une séquence de pliage faisable trouvée par le solveur.</summary>
    public sealed class SolutionPliage
    {
        public List<Operation> Sequence = new();
        public int Retournes;         // nombre de retournements dessus/dessous
        public int ChangementsSens;   // nombre de bascules du sens d'engagement (amont <-> aval)
        public double PriseMini;      // le plus court « ce que l'opérateur tient » de la séquence (mm)
        public string Resume = "";
    }

    /// <summary>
    /// Solveur d'ordre de pliage — recherche EXHAUSTIVE + simulation.
    ///
    /// Principe : on essaie toutes les combinaisons (ordre des plis × sens d'engagement
    /// amont/aval × retournements) et on garde celles qui ne déclenchent AUCUNE collision
    /// bloquante. Le solveur ne recalcule PAS la géométrie : pour chaque candidat il appelle
    /// Moteur.Construire + Detecteur.Analyser — exactement ce que la vue section dessine déjà.
    /// Donc zéro divergence avec l'écran : c'est la vraie géométrie de l'appli, pas un banc.
    ///
    /// FORME CIBLE = la FACE de chaque pli. Face 0 = face de référence (celle du 1er pli) ;
    /// face 1 = face opposée. Deux plis de même face se plient sans retourner ; changer de face
    /// impose un retournement dessus/dessous. C'est la règle du plieur : sur le Z, le 10 et le
    /// 25 sont face NL (0), le 30 est face FL (1) → un seul retournement, imposé entre les deux.
    ///
    /// RÈGLES DE CALAGE (métier, réglables en tête de classe) : flan mini pour former, pan mini
    /// qui porte un retour déjà plié, cote butée mini.
    /// </summary>
    public static class Solveur
    {
        // --- règles de calage (cotes atelier, à ajuster) ---
        public const double MargeEpaule   = 0.0;    // marge en plus de V/2 pour qu'un flan se forme

        /// <summary>
        /// Résout l'ordre de pliage. Retourne les séquences faisables, classées : moins de
        /// retournements d'abord, puis moins de bascules de sens d'engagement.
        /// </summary>
        /// <param name="segments">pans du développé (NbPlis + 1)</param>
        /// <param name="faceParPli">face de chaque pli (0 = référence, 1 = opposée), indexée par n° de ligne</param>
        /// <param name="anglesParPli">angle intérieur cible de chaque pli</param>
        /// <param name="vParPli">ouverture matrice de chaque pli (peut être null → 16)</param>
        public static List<SolutionPliage> Resoudre(
            List<double> segments, int[] faceParPli, double[] anglesParPli, double[] vParPli,
            Plieuse plieuse, Poincon poincon, Matrice matrice, Embase embase,
            double epaisseur = 1.0, double buteeMini = 10.2, int maxRetournes = 3, int maxSolutions = 30)
        {
            int n = Math.Min(faceParPli.Length, Math.Max(0, segments.Count - 1));
            var brutes = new List<SolutionPliage>();
            var faits = new bool[n];
            var seq = new List<(int bend, int face, bool aval)>();
            int gardeFou = 0;
            int masque = 0;                                  // plis déjà faits, en bits

            // Mémoïsation : la géométrie d'une étape dépend de QUELS plis sont faits, pas de
            // l'ORDRE. Même état, même verdict. n! × 2^n -> 2^n × n × 4 (×19 mesuré).
            var vu = new Dictionary<int, bool>(4096);

            void Dfs(int nbFaits, int parite, int retournes)
            {
                if (++gardeFou > 200000) return;                 // sécurité anti-explosion
                if (nbFaits == n) { brutes.Add(Materialiser(seq, segments, anglesParPli, vParPli, retournes)); return; }

                for (int k = 0; k < n; k++)
                {
                    if (faits[k] || faceParPli[k] != parite) continue;
                    for (int f = 0; f < 2; f++)
                    {
                        bool aval = f == 1;

                        // état = (plis faits, pli actif, parité, sens). Même état, même verdict.
                        int cle = (((masque * 16 + k) * 2 + parite) * 2) + (aval ? 1 : 0);
                        if (!vu.TryGetValue(cle, out bool viable))
                        {
                            double vOuv = VDe(matrice, vParPli, k);
                            viable = CalageOk(segments, faits, k, aval, vOuv, buteeMini);
                            if (viable)
                            {
                                var test = SousPiece(segments, epaisseur, seq, k, parite, aval, anglesParPli, vParPli);
                                var etat = Moteur.Construire(test, test.Sequence.Count - 1,
                                                             plieuse, poincon, matrice, embase);
                                viable = !etat.Bloque;          // collision bloquante → branche morte
                            }
                            vu[cle] = viable;
                        }
                        if (!viable) continue;

                        faits[k] = true; masque |= 1 << k;
                        seq.Add((k, parite, aval));
                        Dfs(nbFaits + 1, parite, retournes);
                        seq.RemoveAt(seq.Count - 1);
                        faits[k] = false; masque &= ~(1 << k);
                    }
                }

                if (retournes < maxRetournes)
                {
                    bool resteAutre = false;
                    for (int k = 0; k < n; k++) if (!faits[k] && faceParPli[k] != parite) { resteAutre = true; break; }
                    if (resteAutre) Dfs(nbFaits, 1 - parite, retournes + 1);
                }
            }

            Dfs(0, 0, 0);                                        // départ face de référence
            Array.Clear(faits, 0, n); seq.Clear(); masque = 0;
            Dfs(0, 1, 0);                                        // départ face opposée (pose du flan libre)

            // dédoublonnage + tri
            var vues = new HashSet<string>();
            var uniq = new List<SolutionPliage>();
            foreach (var s in brutes) if (vues.Add(Cle(s))) uniq.Add(s);

            uniq.Sort((a, b) =>
            {
                // 1. le moins de retournements (manutention + risque de rayer le laqué)
                int c = a.Retournes.CompareTo(b.Retournes);
                if (c != 0) return c;
                // 2. RÈGLE MÉTIER : le plus grand côté vers l'opérateur. On classe sur la prise
                //    la plus COURTE de la séquence, la plus grande d'abord : c'est le maillon
                //    faible qui décide, pas la moyenne. Une séquence qui laisse 20 mm en main
                //    part derrière une qui en laisse 200, même si elle a moins de bascules.
                c = b.PriseMini.CompareTo(a.PriseMini);
                if (c != 0) return c;
                // 3. à sécurité égale, le moins de manip
                c = a.ChangementsSens.CompareTo(b.ChangementsSens);
                if (c != 0) return c;
                return a.Sequence.Count.CompareTo(b.Sequence.Count);
            });
            if (uniq.Count > maxSolutions) uniq.RemoveRange(maxSolutions, uniq.Count - maxSolutions);
            return uniq;
        }

        /// <summary>Règles de calage métier : flan mini pour former, pan porteur de retour, butée mini.</summary>
        static bool CalageOk(List<double> segs, bool[] faits, int k, bool aval, double vOuv, double buteeMini)
        {
            int n = segs.Count - 1;
            double amont = segs[k], avalPan = segs[k + 1];
            double epaule = vOuv / 2.0 + MargeEpaule;

            // 1. former : les deux pans au pli doivent couvrir l'épaule du vé
            if (amont < epaule || avalPan < epaule) return false;

            // Pas de règle « 25 mini sur un pan qui porte un retour » : le détecteur la trouve
            // tout seul et mieux (deux 10 refusés à 45/60/90, acceptés à 120/150/170).

            // 3. caler en butée : le pan lu (amont, ou aval si bout pour bout) >= butée mini,
            //    avec la même tolérance que le Detecteur (un pan de 10 se cale en vrai).
            double lu = aval ? avalPan : amont;
            if (lu < buteeMini - Detecteur.TolButee) return false;

            return true;
        }

        /// <summary>
        /// Ce que l'OPÉRATEUR tient devant lui, en mm de développé. Le Moteur range le pan
        /// côté butée à droite et le formage à gauche (opérateur) : donc l'opérateur tient
        /// l'aval en engagement direct, l'amont si la pièce est retournée bout pour bout (⇄).
        /// RÈGLE MÉTIER (Weapon) : « toujours le plus grand côté vers l'opérateur quand c'est
        /// possible ». Tenir un bout de 20 mm, c'est les doigts au poinçon — jamais avec un
        /// intérimaire. Quand on n'a pas le choix, on passe un bras derrière pour soulager au
        /// relâchement : c'est faisable, mais ça se classe en dernier, pas en premier.
        /// </summary>
        public static double PriseOperateur(List<double> segs, int bend, bool aval)
        {
            double s = 0;
            if (aval) { for (int i = 0; i <= bend && i < segs.Count; i++) s += segs[i]; }
            else      { for (int i = bend + 1; i < segs.Count; i++) s += segs[i]; }
            return s;
        }

        static double VDe(Matrice m, double[] vs, int k)
        {
            double v = (vs != null && k < vs.Length && vs[k] > 0) ? vs[k] : 16;
            return m?.VProche(v)?.V ?? v;
        }

        static Piece SousPiece(List<double> segments, double ep,
            List<(int bend, int face, bool aval)> seq, int kAjout, int pariteAjout, bool avalAjout,
            double[] angles, double[] vs)
        {
            var p = new Piece { Epaisseur = ep };
            p.Segments.AddRange(segments);
            foreach (var (bend, face, aval) in seq) AjouterOp(p, bend, face, aval, angles, vs);
            AjouterOp(p, kAjout, pariteAjout, avalAjout, angles, vs);
            return p;
        }

        static void AjouterOp(Piece p, int bend, int face, bool aval, double[] angles, double[] vs)
        {
            p.Sequence.Add(new Operation
            {
                Bend = bend,
                AngleCible = (angles != null && bend < angles.Length) ? angles[bend] : 90,
                Sens = Sens.Haut,                       // sur plieuse le pli actif va TOUJOURS vers le haut
                V = (vs != null && bend < vs.Length && vs[bend] > 0) ? vs[bend] : 16,
                ButeeAval = aval,
                Retournee = face == 1                   // face opposée => pièce retournée à cette étape
            });
        }

        static SolutionPliage Materialiser(List<(int bend, int face, bool aval)> seq,
            List<double> segments, double[] angles, double[] vs, int retournes)
        {
            var sol = new SolutionPliage { Retournes = retournes, PriseMini = double.MaxValue };
            var parts = new List<string>();
            bool? dernierAval = null; int chg = 0;
            foreach (var (bend, face, aval) in seq)
            {
                var op = new Operation
                {
                    Bend = bend,
                    AngleCible = (angles != null && bend < angles.Length) ? angles[bend] : 90,
                    Sens = Sens.Haut,
                    V = (vs != null && bend < vs.Length && vs[bend] > 0) ? vs[bend] : 16,
                    ButeeAval = aval,
                    Retournee = face == 1
                };
                sol.Sequence.Add(op);
                if (dernierAval.HasValue && dernierAval.Value != aval) chg++;
                dernierAval = aval;
                double prise = PriseOperateur(segments, bend, aval);
                if (prise < sol.PriseMini) sol.PriseMini = prise;
                parts.Add($"pli {bend + 1} · {op.AngleCible:0}° · {(aval ? "⇄ aval" : "amont")}{(op.Retournee ? " · ⇅" : "")} · prise {prise:0}");
            }
            if (sol.PriseMini == double.MaxValue) sol.PriseMini = 0;
            sol.ChangementsSens = chg;
            sol.Resume = string.Join("   →   ", parts);
            return sol;
        }

        static string Cle(SolutionPliage s)
        {
            var sb = new StringBuilder();
            foreach (var op in s.Sequence)
                sb.Append(op.Bend).Append(op.ButeeAval ? 'A' : 'a').Append(op.Retournee ? 'R' : 'r').Append('|');
            return sb.ToString();
        }
    }
}
