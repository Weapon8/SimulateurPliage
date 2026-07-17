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
    /// Vue 3D — LA PIÈCE, rien d'autre. Pas d'outillage : un poinçon de 150 à côté d'une boîte
    /// de 66 écrase le cadrage et cache le sujet. L'outil se regarde dans la vue section, qui
    /// le dessine déjà proprement avec le contour figé.
    ///
    /// Deux modes, selon la pièce :
    ///
    ///   SIMPLE (un axe, plis parallèles) — la section de l'étape, extrudée sur la longueur de
    ///   pli. Une couvertine, un Z : une section suffit à décrire toute la longueur.
    ///
    ///   COMPLEXE (plusieurs axes) — le flan en croix, plié volet par volet. Une boîte : le fond
    ///   au milieu, quatre volets autour, chacun portant sa paroi et son rabat. C'est la seule
    ///   vue qui montre la pièce ENTIÈRE — la section, elle, ne montre qu'un axe.
    ///
    /// Convention des pièces complexes : chaque axe est une bande SYMÉTRIQUE dont le pan du
    /// MILIEU est le fond partagé. Sur [20 / 66 / 200 / 66 / 20] le milieu est le 200 ; les pans
    /// de part et d'autre forment les deux volets.
    ///
    /// Pose MACHINE : la presse plie toujours vers le HAUT. Un pli monte donc si sa face est
    /// celle qui est dessus à cette étape, et descend sinon — c'est le ⇅ qui inverse tout.
    /// À la dernière étape la boîte est ouverture en l'air : on la sort, on la retourne.
    ///
    /// Rendu : peintre, faces triées du fond vers l'avant. Vue de côté à hauteur d'œil par
    /// défaut, cadrage FIGÉ sur toutes les étapes — seule la pièce bouge, jamais la caméra.
    /// Souris : glisser = tourner · clic milieu = déplacer · molette = zoom · double-clic = recadrer.
    /// </summary>
    public class VueVolume : Panel
    {
        EtatEtape etat;
        Piece piece;

        const double YawDefaut = 22 * Math.PI / 180;   // de côté, à peine tourné
        const double PitDefaut = 12 * Math.PI / 180;   // hauteur d'œil, pas de plongée

        double _yaw = YawDefaut, _pit = PitDefaut;
        double _zoom = 1, _px, _py;
        Point _drag; MouseButtons _btn = MouseButtons.None;

        double _sc = 1, _cx, _cy; bool _cadre;

        public VueVolume()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Fond;
            Cursor = Cursors.Hand;
        }

        public void Afficher(EtatEtape e, Piece p)
        {
            if (!ReferenceEquals(p, piece)) _cadre = false;    // pièce changée : on recadre
            etat = e; piece = p; Invalidate();
        }

        /// <summary>Gardée pour coller à l'API de VueSection. La 3D ne dessine pas l'outil.</summary>
        public void Outillage(Plieuse pl, Poincon po, Matrice ma, Embase em) { }

        public void Recadrer()
        { _zoom = 1; _px = _py = 0; _yaw = YawDefaut; _pit = PitDefaut; _cadre = false; Invalidate(); }

        // ---------------- facettes ----------------

        sealed class Facette
        {
            public double[][] V;
            public double[] N;
            public Color C;
            public double[][] R;
            public double Prof, Lum;
        }

        double[] Rot(double x, double y, double z)
        {
            double cy = Math.Cos(_yaw), sy = Math.Sin(_yaw);
            double x1 = x * cy + y * sy, y1 = -x * sy + y * cy;
            double cp = Math.Cos(_pit), sp = Math.Sin(_pit);
            return new[] { x1, z * cp - y1 * sp, z * sp + y1 * cp };
        }

        static double[] Normale(double[] a, double[] b, double[] c)
        {
            double[] e1 = { b[0] - a[0], b[1] - a[1], b[2] - a[2] };
            double[] e2 = { c[0] - b[0], c[1] - b[1], c[2] - b[2] };
            double[] n = { e1[1] * e2[2] - e1[2] * e2[1],
                           e1[2] * e2[0] - e1[0] * e2[2],
                           e1[0] * e2[1] - e1[1] * e2[0] };
            double m = Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
            return m > 1e-9 ? new[] { n[0] / m, n[1] / m, n[2] / m } : new[] { 0.0, 0.0, 1.0 };
        }

        /// <summary>Ce pli est-il déjà formé à cette étape ?</summary>
        bool Fait(int axe, int bend, int etape)
        {
            for (int i = 0; i <= etape && i < piece.Sequence.Count; i++)
                if (piece.Sequence[i].Axe == axe && piece.Sequence[i].Bend == bend) return true;
            return false;
        }

        /// <summary>
        /// Sens du virage dans le repère MACHINE. La presse plie toujours vers le haut : un pli
        /// monte si sa face est celle qui est dessus à cette étape, et descend sinon.
        /// </summary>
        static double Tour(Piece bande, int b, bool retournee)
        {
            if (b < 0 || b >= bande.Angles.Count || b >= bande.Faces.Count) return 0;
            double t = (180.0 - bande.Angles[b]) * Math.PI / 180.0;
            return (bande.Faces[b] == retournee) ? t : -t;
        }

        /// <summary>La BOÎTE : le fond au milieu, quatre volets pliés autour.</summary>
        List<Facette> Boite(int etape, bool ret)
        {
            var F = new List<Facette>();
            var bX = piece.Bande(0);
            var bY = piece.Bande(1);
            if (bX.Segments.Count < 3 || bY.Segments.Count < 3) return F;

            double fondX = bX.Segments[bX.Segments.Count / 2];
            double fondY = bY.Segments[bY.Segments.Count / 2];

            F.Add(new Facette
            {
                V = new[] { new[] { -fondX / 2, -fondY / 2, 0.0 }, new[] { fondX / 2, -fondY / 2, 0.0 },
                            new[] { fondX / 2, fondY / 2, 0.0 }, new[] { -fondX / 2, fondY / 2, 0.0 } },
                N = new[] { 0.0, 0.0, 1.0 },
                C = Theme.Tole
            });

            for (int i = 0; i < 4; i++)          // X+ · Y+ · X− · Y−
            {
                int axe = i % 2;
                bool cotePlus = i < 2;
                var bande = piece.Bande(axe);
                int mid = bande.Segments.Count / 2;
                double d0 = bande.Segments[mid] / 2;
                double dm = (axe == 0 ? fondY : fondX) / 2;

                double[] u = i == 0 ? new[] { 1.0, 0.0 } : i == 1 ? new[] { 0.0, 1.0 }
                           : i == 2 ? new[] { -1.0, 0.0 } : new[] { 0.0, -1.0 };
                double[] v = { -u[1], u[0] };

                // profil du volet, de la ligne de pli vers l'extérieur
                var prof = new List<double[]> { new[] { 0.0, 0.0 } };
                double a = 0, f = 0, z = 0;
                int nb = cotePlus ? bande.Segments.Count - mid - 1 : mid;
                for (int k = 0; k < nb; k++)
                {
                    int b = cotePlus ? mid + k : mid - 1 - k;
                    double pan = cotePlus ? bande.Segments[mid + 1 + k] : bande.Segments[mid - 1 - k];
                    if (Fait(axe, b, etape)) a += Tour(bande, b, ret);
                    f += pan * Math.Cos(a); z += pan * Math.Sin(a);
                    prof.Add(new[] { f, z });
                }

                double[] P(double ff, double zz, double t)
                    => new[] { u[0] * (d0 + ff) + v[0] * t, u[1] * (d0 + ff) + v[1] * t, zz };

                for (int k = 0; k + 1 < prof.Count; k++)
                {
                    var A = P(prof[k][0], prof[k][1], -dm);
                    var B = P(prof[k + 1][0], prof[k + 1][1], -dm);
                    var C = P(prof[k + 1][0], prof[k + 1][1], dm);
                    var D = P(prof[k][0], prof[k][1], dm);
                    F.Add(new Facette
                    {
                        V = new[] { A, B, C, D },
                        N = Normale(A, B, C),
                        C = (k == prof.Count - 2 && prof.Count > 2) ? Theme.ToleFL : Theme.Tole
                    });
                }
            }
            return F;
        }

        /// <summary>Le PROFIL simple : la section de l'étape, extrudée sur la longueur de pli.</summary>
        List<Facette> Profil()
        {
            var F = new List<Facette>();
            double demi = Math.Max(25, piece.LongueurPli / 2);

            var ch = new List<Pt>(etat.PanArriere);
            for (int i = 1; i < etat.Formage.Count; i++) ch.Add(etat.Formage[i]);
            if (ch.Count < 2) return F;

            Color c = etat.Bloque ? Theme.Alerte
                    : etat.Op.Reprise ? Theme.Reprise
                    : etat.Op.Retournee ? Theme.ToleFL : Theme.Tole;

            for (int i = 0; i + 1 < ch.Count; i++)
            {
                var A = new[] { ch[i].X, -demi, ch[i].Y };
                var B = new[] { ch[i + 1].X, -demi, ch[i + 1].Y };
                var C = new[] { ch[i + 1].X, demi, ch[i + 1].Y };
                var D = new[] { ch[i].X, demi, ch[i].Y };
                F.Add(new Facette { V = new[] { A, B, C, D }, N = Normale(A, B, C), C = c });
            }
            return F;
        }

        // ---------------- rendu ----------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Fond);

            if (etat?.Op == null || piece == null)
            {
                TextRenderer.DrawText(g, "pas de géométrie à cette étape", Font, new Point(14, 12), Theme.Discret);
                return;
            }

            bool boite = piece.Complexe;
            var F = boite ? Boite(etat.Etape, etat.Op.Retournee) : Profil();
            if (F.Count == 0) return;

            double[] lum = { -0.30, -0.45, 0.84 };
            foreach (var f in F)
            {
                f.R = new double[f.V.Length][];
                double s = 0;
                for (int i = 0; i < f.V.Length; i++)
                { f.R[i] = Rot(f.V[i][0], f.V[i][1], f.V[i][2]); s += f.R[i][2]; }
                f.Prof = s / f.V.Length;
                var n = Rot(f.N[0], f.N[1], f.N[2]);
                f.Lum = 0.38 + 0.62 * Math.Abs(n[0] * lum[0] + n[1] * lum[1] + n[2] * lum[2]);
            }

            if (!_cadre) Cadrer(boite);

            double sc = _sc * _zoom;
            double ox = Width / 2.0 - _cx * sc + _px;
            double oy = Height / 2.0 + _cy * sc + _py;

            F.Sort((a, b) => a.Prof.CompareTo(b.Prof));
            using var pn = new Pen(Color.FromArgb(150, Theme.Fond), 0.9f) { LineJoin = LineJoin.Round };
            foreach (var f in F)
            {
                var pts = new PointF[f.R.Length];
                for (int i = 0; i < f.R.Length; i++)
                    pts[i] = new PointF((float)(ox + f.R[i][0] * sc), (float)(oy - f.R[i][1] * sc));
                using var br = new SolidBrush(Color.FromArgb(
                    (int)(f.C.R * f.Lum), (int)(f.C.G * f.Lum), (int)(f.C.B * f.Lum)));
                g.FillPolygon(br, pts);
                g.DrawPolygon(pn, pts);
            }

            string t = boite
                ? $"étape {etat.Etape + 1}/{piece.Sequence.Count} · axe {(etat.Op.Axe == 0 ? "X" : "Y")} · "
                  + $"pli {etat.Op.Bend + 1} · {etat.Op.AngleCible:0}° · butée {etat.ButeeDistance:0}"
                  + (etat.Etape == piece.Sequence.Count - 1 ? "  —  boîte fermée, ouverture en l'air" : "")
                : $"étape {etat.Etape + 1} · pli {etat.Op.Bend + 1} · {etat.Op.AngleCible:0}° · "
                  + (etat.Op.Retournee ? "FL" : "FNL") + $" · longueur de pli {piece.LongueurPli:0} mm";
            TextRenderer.DrawText(g, t, Font, new Point(14, 12), Theme.Discret);
            TextRenderer.DrawText(g, $"×{_zoom:0.0}  ·  glisser = tourner · molette = zoom · double-clic = vue de côté",
                Font, new Point(14, Height - 24), Theme.Discret);
        }

        /// <summary>
        /// Cadrage FIGÉ, calculé sur TOUTES les étapes réunies : il ne bouge plus d'un pli à
        /// l'autre. Sinon la caméra saute à chaque clic et on croit que la vue tourne.
        /// </summary>
        void Cadrer(bool boite)
        {
            double mnx = double.MaxValue, mxx = double.MinValue, mny = double.MaxValue, mxy = double.MinValue;

            void Mesurer(List<Facette> F)
            {
                foreach (var f in F)
                    foreach (var q in f.V)
                    {
                        var r = Rot(q[0], q[1], q[2]);
                        mnx = Math.Min(mnx, r[0]); mxx = Math.Max(mxx, r[0]);
                        mny = Math.Min(mny, r[1]); mxy = Math.Max(mxy, r[1]);
                    }
            }

            if (boite)
                for (int e = 0; e < piece.Sequence.Count; e++)
                    Mesurer(Boite(e, piece.Sequence[e].Retournee));
            else
                Mesurer(Profil());

            if (mxx <= mnx || mxy <= mny) return;
            _sc = Math.Min((Width - 50) / (mxx - mnx), (Height - 70) / (mxy - mny));
            _cx = (mnx + mxx) / 2; _cy = (mny + mxy) / 2;
            _cadre = true;
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
                    _pit = Math.Max(-1.3, Math.Min(1.4, _pit + dy * 0.011));
                    _cadre = false;
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
