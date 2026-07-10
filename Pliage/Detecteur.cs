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
                foreach (var q in poincon.Contour()) poinconLev.Add(new[] { q[0], q[1] + ep });
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
            bool DansAme(Pt q) => poincon != null && poincon.Contient(q.X, q.Y - ep);

            var segments = Segments(st);

            bool hitP = false, hitM = false, hitPP = false, hitSem = false;
            foreach (var (a, b) in segments)
            {
                if (EnFormage(a) && EnFormage(b)) continue;

                if (!hitP && poinconLev != null && !(DansAme(a) && DansAme(b)) && Croise(a, b, poinconLev))
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

        /// <summary>Segments à tester : tout le pan arrière, et le formage sans son montant droit initial.</summary>
        static List<(Pt, Pt)> Segments(EtatEtape st)
        {
            var segs = new List<(Pt, Pt)>();

            for (int i = 0; i + 1 < st.PanArriere.Count; i++)
                segs.Add((st.PanArriere[i], st.PanArriere[i + 1]));

            if (st.Formage.Count >= 2)
            {
                Pt d0 = Unitaire(st.Formage[1].X - st.Formage[0].X, st.Formage[1].Y - st.Formage[0].Y);
                bool montant = true;
                for (int i = 0; i + 1 < st.Formage.Count; i++)
                {
                    Pt d = Unitaire(st.Formage[i + 1].X - st.Formage[i].X, st.Formage[i + 1].Y - st.Formage[i].Y);
                    if (montant && d.X * d0.X + d.Y * d0.Y > 0.985) continue;
                    montant = false;
                    segs.Add((st.Formage[i], st.Formage[i + 1]));
                }
            }
            return segs;
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

        static bool Intersecte(Pt p1, Pt p2, Pt p3, Pt p4)
        {
            double d1 = Cross(p3, p4, p1), d2 = Cross(p3, p4, p2);
            double d3 = Cross(p1, p2, p3), d4 = Cross(p1, p2, p4);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
                && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        static double Cross(Pt a, Pt b, Pt c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        static Pt Unitaire(double x, double y)
        {
            double m = Math.Sqrt(x * x + y * y);
            return m > 1e-9 ? new Pt(x / m, y / m) : new Pt(0, 0);
        }
    }
}
