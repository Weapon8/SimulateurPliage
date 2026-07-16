using System;
using System.Collections.Generic;
using System.Text;
using SimulateurPliage.Materiel;

namespace SimulateurPliage.Pliage
{
    /// <summary>
    /// Banc de contrôle INTERNE — les règles figées ne sont plus des commentaires,
    /// ce sont des assertions qui tournent dans le build réel.
    ///
    /// RÈGLE 1 (SENS) — figée, jamais rediscutée :
    ///     À TOUTE étape, le pan gauché CONTRE LA BUTÉE est à DROITE (X > 0).
    ///     Le formage part à gauche (opérateur). Sans exception : quelle que soit la
    ///     taille du pan, quel que soit ⇄ (bout pour bout) ou ⇅ (dessus/dessous).
    ///     Point contrôlé = l'extrémité libre du pan qui touche le sommet actif.
    ///
    /// RÈGLE 2 (SOMMET) — le sommet du pli actif est à l'origine (pointe du poinçon).
    ///
    /// Ce fichier ne modifie RIEN : il ne fait que lire Moteur + Detecteur. Si un
    /// contrôle passe au rouge, c'est qu'une modif a cassé une règle — on le voit en
    /// 2 secondes, dans le build, au lieu de six allers-retours de captures.
    /// </summary>
    public static class Autotest
    {
        public static string Executer(Plieuse plieuse, Poincon poincon, Matrice matrice, Embase embase)
        {
            var sb = new StringBuilder();
            int ok = 0, ko = 0;

            Controler(sb, "CHEVÊTRE (référence approuvée)", Piece.Demo(),
                      plieuse, poincon, matrice, embase, ref ok, ref ko);
            sb.AppendLine();
            Controler(sb, "Z LAQUÉ 30·25·25·10", Piece.DemoZLaque(),
                      plieuse, poincon, matrice, embase, ref ok, ref ko);
            sb.AppendLine();
            Controler(sb, "COUVERTINE 10·30·230·30·10 (référence chantier)", Piece.DemoCouvertine(),
                      plieuse, poincon, matrice, embase, ref ok, ref ko);

            string entete = ko == 0
                ? "OK — " + ok + " contrôle(s) passé(s). Les règles tiennent.\r\n\r\n"
                : "ECHEC — " + ko + " contrôle(s) en défaut sur " + (ok + ko) + ".\r\n\r\n";
            sb.Insert(0, entete);
            return sb.ToString();
        }

        static void Controler(StringBuilder sb, string nom, Piece p,
                              Plieuse plieuse, Poincon poincon, Matrice matrice, Embase embase,
                              ref int ok, ref int ko)
        {
            sb.AppendLine("=== " + nom + " ===");

            for (int e = 0; e < p.Sequence.Count; e++)
            {
                var st = Moteur.Construire(p, e, plieuse, poincon, matrice, embase);

                if (st.Op == null || st.PanArriere.Count < 2)
                {
                    sb.AppendLine("  etape " + (e + 1) + " : geometrie vide  <<< ECHEC");
                    ko++;
                    continue;
                }

                // RÈGLE 1 : extrémité libre du pan qui touche le sommet = avant-dernier
                // point du pan arrière (le dernier point EST le sommet, à l'origine).
                Pt libre = st.PanArriere[st.PanArriere.Count - 2];
                bool sensOk = libre.X > 0;
                if (sensOk) ok++; else ko++;

                // RÈGLE 2 : le sommet actif est bien à l'origine.
                Pt sommet = st.PanArriere[st.PanArriere.Count - 1];
                bool sommetOk = Math.Abs(sommet.X) < 0.01 && Math.Abs(sommet.Y) < 0.01;
                if (sommetOk) ok++; else ko++;

                string marque = (st.Op.ButeeAval ? " ⇄" : "") + (st.Op.Retournee ? " ⇅" : "");
                sb.AppendLine("  etape " + (e + 1)
                            + "  pli " + (st.Op.Bend + 1)
                            + "  " + st.Op.AngleCible.ToString("0") + "\u00B0"
                            + marque
                            + "  · butee " + st.ButeeDistance.ToString("0"));

                sb.AppendLine("     R1 sens   : pan butee a " + (libre.X > 0 ? "DROITE" : "GAUCHE")
                            + "  (x=" + libre.X.ToString("0.0") + ")   "
                            + (sensOk ? "ok" : "<<< ECHEC : la regle veut DROITE"));

                sb.AppendLine("     R2 sommet : (" + sommet.X.ToString("0.00") + ", "
                            + sommet.Y.ToString("0.00") + ")   "
                            + (sommetOk ? "ok" : "<<< ECHEC : doit etre a l'origine"));

                sb.AppendLine("     collisions: " + Resume(st.Collisions));
            }
        }

        static string Resume(List<Collision> cols)
        {
            if (cols == null || cols.Count == 0) return "propre";
            var parts = new List<string>();
            foreach (var c in cols) parts.Add(c.Type + (c.Bloquant ? "" : " (avertissement)"));
            return string.Join(" + ", parts);
        }
    }
}
