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
    /// RÈGLE 3 (COTE = DESSIN) — le pan couché à droite contre la butée est bien celui dont
    ///     la cote est affichée : sa longueur dessinée = le pan que donne Piece.PanButee.
    ///     C'est le garde-fou contre le retour de deux règles de butée qui divergent.
    ///
    /// RÈGLE 4 (GAMME) — les cotes de butée des pièces de référence, validées à l'atelier,
    ///     étape par étape : chevêtre 20·20·40·100, Z laqué 10·25·30, couvertine 10·30·10·30,
    ///     chéneau 10·100·30·40·200. Une modif qui en bouge une seule passe au rouge.
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
                      plieuse, poincon, matrice, embase, ref ok, ref ko, new double[] { 20, 20, 40, 100 });
            sb.AppendLine();
            Controler(sb, "Z LAQUÉ 30·25·25·10", Piece.DemoZLaque(),
                      plieuse, poincon, matrice, embase, ref ok, ref ko, new double[] { 10, 25, 30 });
            sb.AppendLine();
            Controler(sb, "COUVERTINE 10·30·230·30·10 (référence chantier)", Piece.DemoCouvertine(),
                      plieuse, poincon, matrice, embase, ref ok, ref ko, new double[] { 10, 30, 10, 30 });
            sb.AppendLine();
            // Le chéneau est la seule référence en FACES SAISIES : c'est lui qui contrôle que la
            // règle « la face commande » tient (côté de butée, sens du dessin, cote).
            Controler(sb, "CHÉNEAU 30·40·150·200·100·10 (faces saisies)", Piece.DemoCheneau(),
                      plieuse, poincon, matrice, embase, ref ok, ref ko, new double[] { 10, 100, 30, 40, 200 });

            string entete = ko == 0
                ? "OK — " + ok + " contrôle(s) passé(s). Les règles tiennent.\r\n\r\n"
                : "ECHEC — " + ko + " contrôle(s) en défaut sur " + (ok + ko) + ".\r\n\r\n";
            sb.Insert(0, entete);
            return sb.ToString();
        }

        static void Controler(StringBuilder sb, string nom, Piece p,
                              Plieuse plieuse, Poincon poincon, Matrice matrice, Embase embase,
                              ref int ok, ref int ko, double[] gamme = null)
        {
            sb.AppendLine("=== " + nom + " ===");

            // Une pièce COMPLEXE porte plusieurs axes : on les contrôle tous. Une boîte, c'est
            // quatre plis, on tourne la pièce d'un quart de tour, quatre plis.
            var axes = p.TousLesAxes();
            for (int ax = 1; ax < axes.Count; ax++)
                Controler(sb, nom + " · axe " + (ax + 1), axes[ax], plieuse, poincon, matrice, embase, ref ok, ref ko);
            if (p.Complexe) sb.AppendLine("  axe 1 :");

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

                // RÈGLE 3 : le pan dessiné contre la butée est celui dont on affiche la cote.
                var bande = p.Bande(st.Op.Axe);
                double lgDessin = Math.Sqrt((libre.X - sommet.X) * (libre.X - sommet.X)
                                          + (libre.Y - sommet.Y) * (libre.Y - sommet.Y));
                double lgPan = bande.Segments[bande.PanButee(st.Op)];
                bool coteOk = Math.Abs(lgDessin - lgPan) < 0.01;
                if (coteOk) ok++; else ko++;
                sb.AppendLine("     R3 cote   : pan en butee dessine " + lgDessin.ToString("0.#")
                            + " / pan lu " + lgPan.ToString("0.#") + "   "
                            + (coteOk ? "ok" : "<<< ECHEC : le dessin et la cote ne parlent pas du meme pan"));

                // RÈGLE 4 : la gamme validée à l'atelier.
                if (gamme != null && e < gamme.Length)
                {
                    bool gammeOk = Math.Abs(st.ButeeDistance - gamme[e]) < 0.01;
                    if (gammeOk) ok++; else ko++;
                    sb.AppendLine("     R4 gamme  : butee " + st.ButeeDistance.ToString("0.#")
                                + " / atelier " + gamme[e].ToString("0.#") + "   "
                                + (gammeOk ? "ok" : "<<< ECHEC : la cote de butee a bouge"));
                }

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
