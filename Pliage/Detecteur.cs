using System;
using System.Collections.Generic;

namespace SimulateurPliage.Pliage
{
    /// <summary>
    /// Détection des collisions entre la pièce et l'outillage, à une étape donnée.
    /// La tôle qui longe l'âme du poinçon n'est PAS une collision : elle se forme
    /// autour de l'outil. Seuls les retours qui reviennent par le flanc comptent.
    /// </summary>
    public static class Detecteur
    {
        public static List<Collision> Analyser(EtatEtape st, Piece p, Materiel.Plieuse plieuse,
                                               Materiel.Poincon poincon, Materiel.Matrice matrice, Materiel.Embase embase)
        {
            var res = new List<Collision>();
            if (st.Op == null) return res;

            double ep = Math.Max(0.2, p.Epaisseur);

            // Le poinçon est remonté de ep : sa pointe touche la face haute de la tôle,
            // sinon le pan posé à plat (y = 0) raserait la pointe.
            List<double[]> poinconLev = null;
            if (poincon != null)
            {
                poinconLev = new List<double[]>();
                foreach (var q in poincon.Contour()) poinconLev.Add(new[] { q[0], q[1] + ep / 2.0 });
            }

            var vf = matrice?.VProche(st.Op.V);
            double vOuv = vf?.V ?? st.Op.V;
            var matriceC = matrice?.Contour(st.Op.V);
            double dieBas = matriceC != null ? Min(matriceC, 1) : -30;

            List<double[]> porteP = null, semelle = null;
            if (embase != null)
            {
                double haut = (poincon?.Hauteur ?? 120) + ep;
                if (embase.PortePoinconLg > 0 && embase.PortePoinconH > 0)
                    porteP = Rect(-embase.PortePoinconLg / 2, haut, embase.PortePoinconLg / 2, haut + embase.PortePoinconH);
                if (embase.SemelleLg > 0 && embase.SemelleH > 0)
                    semelle = Rect(-embase.SemelleLg / 2, dieBas - embase.SemelleH, embase.SemelleLg / 2, dieBas);
            }

            // Zone morte : autour de la pointe, dans le vé, la tôle est en cours de formage.
            double zoneX = vOuv / 2.0 + ep + 4;
            double zoneY = ep + 6;
            bool EnFormage(Pt q) => q.Y <= zoneY && q.Y >= -2 && Math.Abs(q.X) <= zoneX;

            // Dans l'âme du poinçon : la tôle épouse l'outil, pas une collision.
            bool DansAme(Pt q) => poincon != null && poincon.Contient(q.X, q.Y - ep / 2.0);

            var segments = Segments(st);

            // Autour du sommet, la tole s'enroule sur la pointe : tout croisement dans ce
            // rayon est du formage, pas une collision. (Sommet vif cote tole, pointe ronde
            // cote outil : sans ca, chaque pli sort un faux positif.)
            double rayonMort = ep + (poincon?.R ?? 1.0) + 2.0;

            bool hitP = false, hitM = false, hitPP = false, hitSem = false;
            foreach (var (a, b) in segments)
            {
                if (EnFormage(a) && EnFormage(b)) continue;

                if (!hitP && poinconLev != null && !(DansAme(a) && DansAme(b))
                    && CroiseHorsPointe(a, b, poinconLev, rayonMort))
                    hitP = true;
                if (!hitM && matriceC != null && (a.Y < -0.6 || b.Y < -0.6) && Croise(a, b, matriceC))
                    hitM = true;
                if (!hitPP && porteP != null && Croise(a, b, porteP)) hitPP = true;
                if (!hitSem && semelle != null && Croise(a, b, semelle)) hitSem = true;
            }

            if (hitP) res.Add(new Collision("poinçon", "un retour de tôle tape le poinçon", true));
            if (hitM) res.Add(new Collision("matrice", "la tôle tape la matrice", true));
            if (hitPP) res.Add(new Collision("porte-poinçon", "un retour touche l'embase du poinçon", true));
            if (hitSem) res.Add(new Collision("semelle", "la pièce touche la semelle", true));

            if (ReplieSurElleMeme(st))
                res.Add(new Collision("repli sur repli", "la pièce se referme sur elle-même", true));

            if (plieuse != null && st.ButeeDistance > plieuse.ButeeMax)
                res.Add(new Collision("butée arrière",
                    $"pan de {st.ButeeDistance:0} mm > course butée {plieuse.ButeeMax:0} mm", false));

            if (plieuse != null && st.ButeeDistance > 0 && st.ButeeDistance < plieuse.ButeeMin)
                res.Add(new Collision("butée arrière",
                    $"pan de {st.ButeeDistance:0} mm < butée mini {plieuse.ButeeMin:0.#} mm", false));

            return res;
        }

        /// <summary>
        /// Segments à tester = les RETOURS DÉJÀ PLIÉS, uniquement. De chaque côté du sommet
        /// actif on saute la partie rectiligne du bras en cours de formage : elle épouse la
        /// pointe, ce n'est jamais une collision. Un « retour » n'existe qu'AU-DELÀ du premier
        /// coude. Conséquence directe : au tout premier pli il n'y a aucun coude antérieur,
        /// donc aucun segment testé — rien ne peut « taper le poinçon », ce qui est la réalité
        /// (un bec à 35° laisse le champ libre à un pli de 45°).
        /// </summary>
        static List<(Pt, Pt)> Segments(EtatEtape st)
        {
            var segs = new List<(Pt, Pt)>();
            ApresMontant(segs, st.PanArriere, sommetEnFin: true);   // pan couché : sommet = dernier point
            ApresMontant(segs, st.Formage,    sommetEnFin: false);  // formage    : sommet = premier point
            return segs;
        }

        /// <summary>Ajoute les segments d'un pan en partant DU SOMMET vers l'extérieur, en
        /// sautant le montant rectiligne initial (le bras droit, non plié, du pli actif).</summary>
        static void ApresMontant(List<(Pt, Pt)> segs, List<Pt> pan, bool sommetEnFin)
        {
            int n = pan.Count;
            if (n < 2) return;
            var ordre = new int[n];
            for (int k = 0; k < n; k++) ordre[k] = sommetEnFin ? n - 1 - k : k;

            Pt d0 = Unitaire(pan[ordre[1]].X - pan[ordre[0]].X, pan[ordre[1]].Y - pan[ordre[0]].Y);
            bool montant = true;
            for (int k = 0; k + 1 < n; k++)
            {
                int a = ordre[k], b = ordre[k + 1];
                Pt d = Unitaire(pan[b].X - pan[a].X, pan[b].Y - pan[a].Y);
                if (montant && d.X * d0.X + d.Y * d0.Y > 0.985) continue;   // encore le bras droit
                montant = false;
                segs.Add((pan[a], pan[b]));
            }
        }

        static bool ReplieSurElleMeme(EtatEtape st)
        {
            var poly = new List<Pt>(st.PanArriere);
            for (int i = 1; i < st.Formage.Count; i++) poly.Add(st.Formage[i]);

            for (int i = 0; i + 1 < poly.Count; i++)
                for (int j = i + 2; j + 1 < poly.Count; j++)
                {
                    if (i == 0 && j == poly.Count - 2) continue;
                    if (Intersecte(poly[i], poly[i + 1], poly[j], poly[j + 1])) return true;
                }
            return false;
        }

        // ---- primitives géométriques ----

        static List<double[]> Rect(double x0, double y0, double x1, double y1) => new()
        {
            new[] { x0, y0 }, new[] { x1, y0 }, new[] { x1, y1 }, new[] { x0, y1 }
        };

        static double Min(List<double[]> poly, int axe)
        {
            double m = double.MaxValue;
            foreach (var p in poly) m = Math.Min(m, p[axe]);
            return m;
        }

        static bool Croise(Pt a, Pt b, List<double[]> poly)
        {
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if (Intersecte(a, b, new Pt(poly[j][0], poly[j][1]), new Pt(poly[i][0], poly[i][1])))
                    return true;
            return false;
        }

        /// <summary>Croisement reel, en ignorant ceux qui tombent dans le rayon de formage
        /// autour du sommet du pli (origine du repere).</summary>
        static bool CroiseHorsPointe(Pt a, Pt b, List<double[]> poly, double rayonMort)
        {
            double r2 = rayonMort * rayonMort;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var p3 = new Pt(poly[j][0], poly[j][1]);
                var p4 = new Pt(poly[i][0], poly[i][1]);
                if (!Intersecte(a, b, p3, p4)) continue;
                if (Point(a, b, p3, p4, out var ip) && ip.X * ip.X + ip.Y * ip.Y <= r2) continue;
                return true;
            }
            return false;
        }

        static bool Point(Pt p1, Pt p2, Pt p3, Pt p4, out Pt ip)
        {
            ip = default;
            double d = (p2.X - p1.X) * (p4.Y - p3.Y) - (p2.Y - p1.Y) * (p4.X - p3.X);
            if (Math.Abs(d) < 1e-12) return false;
            double t = ((p3.X - p1.X) * (p4.Y - p3.Y) - (p3.Y - p1.Y) * (p4.X - p3.X)) / d;
            ip = new Pt(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
            return true;
        }

        static bool Intersecte(Pt p1, Pt p2, Pt p3, Pt p4)
        {
            double d1 = Cross(p3, p4, p1), d2 = Cross(p3, p4, p2);
            double d3 = Cross(p1, p2, p3), d4 = Cross(p1, p2, p4);
            const double e = 1e-7;   // colinéaire / tangent -> PAS un croisement (évite un faux « repli »)
            return ((d1 > e && d2 < -e) || (d1 < -e && d2 > e))
                && ((d3 > e && d4 < -e) || (d3 < -e && d4 > e));
        }

        static double Cross(Pt a, Pt b, Pt c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        static Pt Unitaire(double x, double y)
        {
            double m = Math.Sqrt(x * x + y * y);
            return m > 1e-9 ? new Pt(x / m, y / m) : new Pt(0, 0);
        }
    }
}
