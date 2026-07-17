using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SimulateurPliage.Materiel;
using SimulateurPliage.Pliage;

namespace SimulateurPliage.Vues
{
    /// <summary>
    /// Vue 3D. La section de l'étape est EXTRUDÉE sur la longueur de pli — l'outillage est
    /// prismatique, donc une section suffit à décrire toute la longueur.
    ///
    /// Elle lit le même EtatEtape que la vue section : rien de recalculé, aucune géométrie
    /// dupliquée. Si les deux vues divergent un jour, c'est un bug.
    ///
    /// Rendu : peintre — on trie les faces du fond vers l'avant et on les peint dans l'ordre.
    /// Pas de moteur 3D, pas de dépendance. Sa limite : deux faces qui se TRAVERSENT sortent
    /// mal. Ici l'outillage et la tôle se touchent sans se pénétrer, donc ça tient.
    ///
    /// Souris : glisser = tourner · clic milieu = déplacer · molette = zoomer · double-clic = recadrer.
    /// </summary>
    public class VueVolume : Panel
    {
        EtatEtape etat;
        Piece piece;
        Plieuse plieuse;
        Poincon poincon;
        Matrice matrice;

        double _yaw = 34 * Math.PI / 180, _pit = 20 * Math.PI / 180;
        double _zoom = 1, _px, _py;
        Point _drag; MouseButtons _btn = MouseButtons.None;

        /// <summary>Recul caméra, en mm. Plus c'est court, plus la fuite est marquée.</summary>
        public double Recul = 2200;

        /// <summary>Épaisseur dessinée mini, en px : 1 mm de tôle est invisible à cette échelle.</summary>
        const double EpaisseurMiniPx = 6.0;

        public VueVolume()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Fond;
            Cursor = Cursors.Hand;
        }

        public void Afficher(EtatEtape e, Piece p) { etat = e; piece = p; Invalidate(); }

        public void Outillage(Plieuse pl, Poincon po, Matrice ma, Embase em)
        { plieuse = pl; poincon = po; matrice = ma; Invalidate(); }

        public void Recadrer() { _zoom = 1; _px = _py = 0; _yaw = 34 * Math.PI / 180; _pit = 20 * Math.PI / 180; Invalidate(); }

        // ---------------- géométrie ----------------

        /// <summary>Une facette : ses sommets 3D, sa normale, sa couleur.</summary>
        sealed class Facette
        {
            public double[][] V;      // sommets (x,y,z)
            public double[] N;        // normale
            public Color C;
            public double[][] R;      // sommets tournés
            public double Prof;       // profondeur moyenne
            public double Lum;        // 0..1
        }

        double[] Rot(double x, double y, double z)
        {
            double cx = Math.Cos(_yaw), sx = Math.Sin(_yaw);
            double x1 = x * cx + z * sx, z1 = -x * sx + z * cx;
            double cy = Math.Cos(_pit), sy = Math.Sin(_pit);
            return new[] { x1, y * cy - z1 * sy, y * sy + z1 * cy };
        }

        /// <summary>Aire signée : >0 anti-horaire, <0 horaire.</summary>
        static double Aire(IList<double[]> c)
        {
            double a = 0;
            for (int i = 0; i < c.Count; i++)
            {
                var p = c[i]; var q = c[(i + 1) % c.Count];
                a += p[0] * q[1] - q[0] * p[1];
            }
            return a / 2;
        }

        /// <summary>
        /// Extrude un contour fermé entre z0 et z1 : les flancs + les deux bouchons.
        /// La normale d'un flanc dépend du SENS du contour : (dy,-dx) sort d'un contour
        /// anti-horaire mais RENTRE dans un horaire. Les contours d'outillage sont horaires,
        /// la section de tôle peut être des deux — on lit l'aire signée et on oriente en
        /// conséquence. Sans ça les faces sont éclairées par derrière et tout paraît terne.
        /// </summary>
        static void Extruder(List<Facette> f, IList<double[]> c, double z0, double z1, Color col)
        {
            int n = c.Count;
            if (n < 3) return;
            double sens = Aire(c) >= 0 ? 1.0 : -1.0;
            for (int i = 0; i < n; i++)
            {
                var p0 = c[i]; var p1 = c[(i + 1) % n];
                double dx = p1[0] - p0[0], dy = p1[1] - p0[1];
                double L = Math.Sqrt(dx * dx + dy * dy);
                if (L < 1e-9) continue;
                f.Add(new Facette
                {
                    V = new[] { new[] { p0[0], p0[1], z0 }, new[] { p1[0], p1[1], z0 },
                                new[] { p1[0], p1[1], z1 }, new[] { p0[0], p0[1], z1 } },
                    N = new[] { sens * dy / L, -sens * dx / L, 0.0 },
                    C = col
                });
            }
            foreach (var (z, s) in new[] { (z0, -1.0), (z1, 1.0) })
            {
                var v = new double[n][];
                for (int i = 0; i < n; i++) v[i] = new[] { c[i][0], c[i][1], z };
                f.Add(new Facette { V = v, N = new[] { 0.0, 0.0, s }, C = col });
            }
        }

        /// <summary>Décale une polyligne le long de sa NORMALE (pas en Y) — même logique que la
        /// vue section : anti-pic sur les plis serrés, sinon les bords se croisent au sommet.</summary>
        static List<double[]> Offset(List<Pt> pts, double d)
        {
            var res = new List<double[]>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                double[] n0 = null, n1 = null;
                if (i > 0) { var t = U(pts[i].X - pts[i - 1].X, pts[i].Y - pts[i - 1].Y); n0 = new[] { t[1], -t[0] }; }
                if (i < pts.Count - 1) { var t = U(pts[i + 1].X - pts[i].X, pts[i + 1].Y - pts[i].Y); n1 = new[] { t[1], -t[0] }; }
                double[] m;
                if (n0 == null) m = n1;
                else if (n1 == null) m = n0;
                else
                {
                    var mm = U(n0[0] + n1[0], n0[1] + n1[1]);
                    double k = Math.Max(0.3, mm[0] * n0[0] + mm[1] * n0[1]);
                    m = new[] { mm[0] / k, mm[1] / k };
                }
                res.Add(new[] { pts[i].X + m[0] * d, pts[i].Y + m[1] * d });
            }
            return res;
        }

        static double[] U(double x, double y)
        { double m = Math.Sqrt(x * x + y * y); return m > 1e-9 ? new[] { x / m, y / m } : new[] { 0.0, 0.0 }; }

        static double[][] Doigt(double bd, double ht, double hc)
            => new[] { new[] { bd, ht }, new[] { bd + 8, ht }, new[] { bd + 8, 6.0 },
                       new[] { bd + 26, 6.0 }, new[] { bd + 26, 0.0 }, new[] { bd, 0.0 } };

        // ---------------- rendu ----------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Fond);

            if (etat?.Op == null || piece == null || etat.PanArriere.Count < 2)
            {
                TextRenderer.DrawText(g, "pas de géométrie à cette étape", Font,
                    new Point(14, 12), Theme.Discret);
                return;
            }

            double ep = Math.Max(0.2, piece.Epaisseur);
            double lg = Math.Max(50, piece.LongueurPli);
            double demiT = lg / 2, demiO = demiT + 60;        // l'outillage déborde la tôle

            // la chaîne complète, sous-face posée sur la face matrice
            var chaine = new List<Pt>();
            foreach (var p in etat.PanArriere) chaine.Add(new Pt(p.X, p.Y + ep / 2));
            for (int i = 1; i < etat.Formage.Count; i++)
                chaine.Add(new Pt(etat.Formage[i].X, etat.Formage[i].Y + ep / 2));

            var F = new List<Facette>();

            if (matrice != null)
            {
                var c = matrice.Contour(etat.Op.V);
                Extruder(F, c, -demiO, demiO, Theme.Matrice);
            }

            // épaisseur dessinée : la vraie, jamais moins de EpaisseurMiniPx à l'écran.
            // Elle sert AUSSI à poser le poinçon : sa pointe touche la face haute, pas le milieu.
            double sc0 = Echelle(F, chaine, ep, demiT, demiO);
            double epVue = Math.Max(ep, EpaisseurMiniPx / Math.Max(sc0, 1e-6));

            if (poincon != null)
            {
                var c = poincon.Contour();
                var lev = new List<double[]>(c.Count);
                foreach (var q in c) lev.Add(new[] { q[0], q[1] + epVue });
                Extruder(F, lev, -demiO, demiO, Theme.Outil);
            }

            // les DOIGTS de butée : deux, pas une barre. Ils coulissent latéralement.
            double bd = etat.ButeeDistance;
            if (plieuse != null && bd > 0)
            {
                double ht = plieuse.DoigtHauteur > 0 ? plieuse.DoigtHauteur : 35;
                double hc = plieuse.DoigtContact > 0 ? plieuse.DoigtContact : 10;
                double dz = Math.Min(demiT * 0.6, 150), dw = 11;
                foreach (double z in new[] { dz, -dz })
                {
                    Extruder(F, Doigt(bd, ht, hc), z - dw, z + dw, Theme.Embase);
                    F.Add(new Facette      // la face d'appui, en orange : c'est elle qui touche
                    {
                        V = new[] { new[] { bd, 0.0, z - dw }, new[] { bd, hc, z - dw },
                                    new[] { bd, hc, z + dw }, new[] { bd, 0.0, z + dw } },
                        N = new[] { -1.0, 0.0, 0.0 },
                        C = Theme.Accent
                    });
                }
            }

            // la tôle : section fermée (dessus + dessous inversé), puis extrudée
            Color ct = etat.Bloque ? Theme.Alerte
                     : etat.Op.Reprise ? Theme.Reprise
                     : etat.Op.Retournee ? Theme.ToleFL : Theme.Tole;
            var haut = Offset(chaine, -ep / 2 + epVue);
            var bas = Offset(chaine, -ep / 2);
            var sect = new List<double[]>(haut);
            for (int i = bas.Count - 1; i >= 0; i--) sect.Add(bas[i]);
            Extruder(F, sect, -demiT, demiT, ct);

            // tourner, éclairer, mesurer
            double[] lum = { -0.36, 0.60, 0.72 };
            foreach (var f in F)
            {
                f.R = new double[f.V.Length][];
                double s = 0;
                for (int i = 0; i < f.V.Length; i++) { f.R[i] = Rot(f.V[i][0], f.V[i][1], f.V[i][2]); s += f.R[i][2]; }
                f.Prof = s / f.V.Length;
                var n = Rot(f.N[0], f.N[1], f.N[2]);
                f.Lum = 0.40 + 0.60 * Math.Max(0, n[0] * lum[0] + n[1] * lum[1] + n[2] * lum[2]);
            }

            double minx = double.MaxValue, maxx = double.MinValue, miny = double.MaxValue, maxy = double.MinValue;
            foreach (var f in F)
                foreach (var q in f.R)
                {
                    double k = Fuite(q[2]);
                    minx = Math.Min(minx, q[0] * k); maxx = Math.Max(maxx, q[0] * k);
                    miny = Math.Min(miny, q[1] * k); maxy = Math.Max(maxy, q[1] * k);
                }
            if (maxx <= minx || maxy <= miny) return;

            double sc = Math.Min((Width - 40) / (maxx - minx), (Height - 40) / (maxy - miny)) * _zoom;
            double ox = Width / 2.0 - (minx + (maxx - minx) / 2) * sc + _px;
            double oy = Height / 2.0 + (miny + (maxy - miny) / 2) * sc + _py;

            // peintre : du fond vers l'avant
            F.Sort((a, b) => a.Prof.CompareTo(b.Prof));
            using var pn = new Pen(Color.FromArgb(140, Theme.Fond), 0.8f) { LineJoin = LineJoin.Round };
            foreach (var f in F)
            {
                var pts = new PointF[f.R.Length];
                for (int i = 0; i < f.R.Length; i++)
                {
                    double k = Fuite(f.R[i][2]);
                    pts[i] = new PointF((float)(ox + f.R[i][0] * k * sc), (float)(oy - f.R[i][1] * k * sc));
                }
                using var br = new SolidBrush(Color.FromArgb(
                    (int)(f.C.R * f.Lum), (int)(f.C.G * f.Lum), (int)(f.C.B * f.Lum)));
                g.FillPolygon(br, pts);
                g.DrawPolygon(pn, pts);
            }

            string t = $"étape {etat.Etape + 1} · pli {etat.Op.Bend + 1} · {etat.Op.AngleCible:0}° · "
                     + (etat.Op.Retournee ? "FL" : "FNL") + $" · butée {bd:0} · longueur de pli {lg:0} mm";
            TextRenderer.DrawText(g, t, Font, new Point(14, 12), Theme.Discret);
            TextRenderer.DrawText(g, $"×{_zoom:0.0}  ·  glisser = tourner · molette = zoom · double-clic = recadrer",
                Font, new Point(14, Height - 24), Theme.Discret);
        }

        /// <summary>Facteur de fuite. Recul grand = quasi orthographique.</summary>
        double Fuite(double z) => Recul / Math.Max(80, Recul - z);

        /// <summary>Échelle approchée, juste pour dimensionner l'épaisseur dessinée.</summary>
        double Echelle(List<Facette> f, List<Pt> chaine, double ep, double demiT, double demiO)
        {
            double mnx = double.MaxValue, mxx = double.MinValue, mny = double.MaxValue, mxy = double.MinValue;
            foreach (var p in chaine)
            {
                mnx = Math.Min(mnx, p.X); mxx = Math.Max(mxx, p.X);
                mny = Math.Min(mny, p.Y); mxy = Math.Max(mxy, p.Y);
            }
            foreach (var x in f)
                foreach (var q in x.V)
                {
                    mnx = Math.Min(mnx, q[0]); mxx = Math.Max(mxx, q[0]);
                    mny = Math.Min(mny, q[1]); mxy = Math.Max(mxy, q[1]);
                }
            double w = Math.Max(1, mxx - mnx), h = Math.Max(1, mxy - mny);
            return Math.Min((Width - 40) / w, (Height - 40) / h) * _zoom;
        }

        // ---------------- souris ----------------

        protected override void OnMouseDown(MouseEventArgs e)
        { _drag = e.Location; _btn = e.Button; Focus(); base.OnMouseDown(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        { _btn = MouseButtons.None; base.OnMouseUp(e); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_btn != MouseButtons.None)
            {
                int dx = e.X - _drag.X, dy = e.Y - _drag.Y;
                if (_btn == MouseButtons.Middle) { _px += dx; _py += dy; }
                else
                {
                    _yaw += dx * 0.011;
                    _pit = Math.Max(-1.1, Math.Min(1.3, _pit + dy * 0.011));
                }
                _drag = e.Location;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _zoom = Math.Max(0.4, Math.Min(8, _zoom * (e.Delta > 0 ? 1.15 : 0.87)));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e) { Recadrer(); base.OnMouseDoubleClick(e); }
    }
}
