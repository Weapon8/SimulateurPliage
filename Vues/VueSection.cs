using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SimulateurPliage.Materiel;
using SimulateurPliage.Pliage;

namespace SimulateurPliage.Vues
{
    /// <summary>Vue de section : outillage + tôle, dans le repère ancré sur le pli actif.</summary>
    public class VueSection : Panel
    {
        EtatEtape etat;
        Piece piece;
        Plieuse plieuse;
        Poincon poincon;
        Matrice matrice;
        Embase embase;

        double sc = 1, ox, oy;

        public VueSection()
        {
            DoubleBuffered = true;
            BackColor = Theme.Fond;
        }

        public void Afficher(EtatEtape e, Piece p) { etat = e; piece = p; Invalidate(); }

        public void Outillage(Plieuse pl, Poincon po, Matrice ma, Embase em)
        { plieuse = pl; poincon = po; matrice = ma; embase = em; Invalidate(); }

        // ═══════════════════════════════════════════════════════════════════════
        //  SENS VERROUILLÉ — NE JAMAIS MODIFIER / NE JAMAIS MIROITER
        //  Poinçon (col de cygne) + butée = À DROITE. Tôle se développe vers la GAUCHE.
        //  Toutes les plieuses sont bâties ainsi. Aucun paramètre de miroir.
        // ═══════════════════════════════════════════════════════════════════════
        PointF T(double x, double y) => new((float)(ox + x * sc), (float)(oy - y * sc));
        PointF T(Pt p) => T(p.X, p.Y);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Fond);

            if (etat?.Op == null) { Centre(g, "Ajoute des plis et une séquence"); return; }

            double ep = Math.Max(0.2, piece.Epaisseur);
            // Ancrage bissectrice : le sommet du pli est a l'origine, sous la pointe du
            // poincon. On remonte la fibre neutre d'une demi-epaisseur : la SOUS-FACE de la
            // tole pose alors sur la face matrice (y = 0) au lieu d'etre a moitie dans le bloc.
            // Le poincon est deja dessine a +ep/2 : sa pointe touche la face haute. Coherent.
            double assise = ep / 2.0;

            var matriceC = matrice?.Contour(etat.Op.V);
            var poinconC = poincon?.Contour();
            double hLibre = plieuse?.HauteurLibre ?? 120;

            if (!Cadrer(g, ep, assise, matriceC, poinconC, hLibre)) return;

            Grille(g);
            DessinerEmbases(g, ep, matriceC);
            if (matriceC != null) Polygone(g, matriceC, 0, Theme.Matrice, Color.FromArgb(110, 120, 132));
            if (poinconC != null) Polygone(g, poinconC, ep / 2.0, Theme.Outil, Color.FromArgb(130, 138, 150));
            HauteurLibre(g, hLibre);
            DessinerTole(g, ep, assise);
            DessinerButee(g);
            DessinerSigles(g);
            Legende(g);
        }

        bool Cadrer(Graphics g, double ep, double assise,
                    List<double[]> matriceC, List<double[]> poinconC, double hLibre)
        {
            double minX = -40, maxX = 40, minY = -40, maxY = hLibre;
            void Acc(double x, double y)
            {
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
            }

            foreach (var q in etat.PanArriere) Acc(q.X, q.Y + assise);
            foreach (var q in etat.Formage) Acc(q.X, q.Y + assise);
            if (matriceC != null) foreach (var p in matriceC) Acc(p[0], p[1]);
            if (poinconC != null) foreach (var p in poinconC) Acc(p[0], p[1] + ep / 2.0);

            double dx = Math.Max(1, maxX - minX), dy = Math.Max(1, maxY - minY);
            const double m = 40;
            sc = Math.Min((Width - 2 * m) / dx, (Height - 2 * m) / dy);
            if (sc <= 0 || double.IsInfinity(sc)) return false;

            ox = m - minX * sc + (Width - 2 * m - dx * sc) / 2;
            oy = Height - m + minY * sc - (Height - 2 * m - dy * sc) / 2;
            return true;
        }

        void DessinerEmbases(Graphics g, double ep, List<double[]> matriceC)
        {
            if (embase == null) return;
            using var b = new SolidBrush(Theme.Embase);

            if (embase.PortePoinconLg > 0 && embase.PortePoinconH > 0)
            {
                double haut = (poincon?.Hauteur ?? 120) + ep / 2.0;
                double w = embase.PortePoinconLg / 2;
                g.FillPolygon(b, new[]
                {
                    T(-w, haut), T(w, haut),
                    T(w, haut + embase.PortePoinconH), T(-w, haut + embase.PortePoinconH)
                });
            }

            if (matriceC != null && embase.SemelleLg > 0 && embase.SemelleH > 0)
            {
                double bas = double.MaxValue;
                foreach (var p in matriceC) bas = Math.Min(bas, p[1]);
                double w = embase.SemelleLg / 2;
                g.FillPolygon(b, new[]
                {
                    T(-w, bas), T(w, bas),
                    T(w, bas - embase.SemelleH), T(-w, bas - embase.SemelleH)
                });
            }
        }

        void Polygone(Graphics g, List<double[]> pts, double decalageY, Color fond, Color bord)
        {
            var poly = new PointF[pts.Count];
            for (int i = 0; i < pts.Count; i++) poly[i] = T(pts[i][0], pts[i][1] + decalageY);
            using var b = new SolidBrush(fond);
            using var pn = new Pen(bord, 1.3f);
            g.FillPolygon(b, poly);
            g.DrawPolygon(pn, poly);
        }

        void HauteurLibre(Graphics g, double h)
        {
            using var pn = new Pen(Color.FromArgb(90, 98, 110), 1f) { DashStyle = DashStyle.Dash };
            using var f = new Font("Segoe UI", 8);
            var a = T(-1000, h);
            g.DrawLine(pn, 0, a.Y, Width, a.Y);
            g.DrawString($"hauteur libre {h:0}", f, new SolidBrush(Theme.Discret), 14, a.Y - 16);
        }

        void DessinerTole(Graphics g, double ep, double assise)
        {
            var chaine = new List<Pt>();
            foreach (var p in etat.PanArriere) chaine.Add(new Pt(p.X, p.Y + assise));
            for (int i = 1; i < etat.Formage.Count; i++)
                chaine.Add(new Pt(etat.Formage[i].X, etat.Formage[i].Y + assise));
            if (chaine.Count < 2) return;

            // La couleur DIT quelle face est dessus : bleu = FNL (face de référence, laquage
            // protégé dessous), violet = FL (pièce retournée, le laquage est visible — on y
            // fait gaffe). Collision et reprise passent devant : ce sont des alertes.
            Color col = etat.Bloque ? Theme.Alerte
                      : etat.Op.Reprise ? Theme.Reprise
                      : etat.Op.Retournee ? Theme.ToleFL
                      : Theme.Tole;

            // La tole est un CORPS, pas un trait : remplissage + contour net, pour qu'on VOIE
            // qu'elle pose sur l'outillage. L'ancien trait de 3 px peint par-dessus avalait le
            // ruban et la faisait lire comme une ligne.
            // Le champ fait ~230 mm de haut (poincon 150 + matrice 80) : 1 mm de tole y vaut
            // 3 px. On garantit donc une epaisseur DESSINEE minimale, comme sur un plan de
            // coupe. On epaissit vers le HAUT : la sous-face reste posee sur la face matrice
            // (y = 0 grace a l'assise), jamais dans le bloc.
            double epVue = Math.Max(ep, EpaisseurMiniPx / Math.Max(sc, 1e-6));
            var inte = Offset(chaine, -ep / 2);                 // sous-face reelle
            var ext  = Offset(chaine, -ep / 2 + epVue);         // face haute, epaissie si besoin

            var ruban = new List<PointF>(ext.Count + inte.Count);
            foreach (var p in ext) ruban.Add(T(p));
            for (int i = inte.Count - 1; i >= 0; i--) ruban.Add(T(inte[i]));
            var poly = ruban.ToArray();

            using (var b = new SolidBrush(Color.FromArgb(175, col)))
                g.FillPolygon(b, poly);
            using (var pn = new Pen(col, 1.6f) { LineJoin = LineJoin.Round })
                g.DrawPolygon(pn, poly);

            using (var b = new SolidBrush(col))
            {
                var o = T(0, assise);
                g.FillEllipse(b, o.X - 3.5f, o.Y - 3.5f, 7, 7);   // sommet du pli actif
            }
        }

        /// <summary>Offset d'une polyligne, jointures mitrées (plis francs, sans rayon).</summary>
        static List<Pt> Offset(List<Pt> pts, double d)
        {
            var res = new List<Pt>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                Pt p = pts[i];
                Pt n0 = default, n1 = default;
                bool a = i > 0, b = i < pts.Count - 1;

                if (a) { var t = Unit(p.X - pts[i - 1].X, p.Y - pts[i - 1].Y); n0 = new Pt(t.Y, -t.X); }
                if (b) { var t = Unit(pts[i + 1].X - p.X, pts[i + 1].Y - p.Y); n1 = new Pt(t.Y, -t.X); }

                Pt m;
                if (!a) m = n1;
                else if (!b) m = n0;
                else
                {
                    var mm = Unit(n0.X + n1.X, n0.Y + n1.Y);
                    double c = Math.Max(0.25, mm.X * n0.X + mm.Y * n0.Y);  // anti-pic sur plis serrés
                    m = new Pt(mm.X / c, mm.Y / c);
                }
                res.Add(new Pt(p.X + m.X * d, p.Y + m.Y * d));
            }
            return res;
        }

        static Pt Unit(double x, double y)
        {
            double m = Math.Sqrt(x * x + y * y);
            return m > 1e-9 ? new Pt(x / m, y / m) : new Pt(0, 0);
        }

        void Grille(Graphics g)
        {
            using var pn = new Pen(Theme.Grille, 1f);
            for (int x = 0; x < Width; x += 40) g.DrawLine(pn, x, 0, x, Height);
            for (int y = 0; y < Height; y += 40) g.DrawLine(pn, 0, y, Width, y);
        }

        /// <summary>
        /// Butée arrière (côté droit) : doigt en L posé au niveau de la face matrice à la
        /// cote lue — pied qui s'étend vers l'arrière, face d'appui basse (0 → DoigtContact,
        /// ≈10 mm) mise en évidence en orange (là où le bord de tôle bute), corps qui remonte
        /// jusqu'à DoigtHauteur avec l'axe rond en tête. Se décale d'étape en étape.
        /// </summary>
        void DessinerButee(Graphics g)
        {
            if (etat?.Op == null || etat.ButeeDistance <= 0) return;

            double bd = etat.ButeeDistance;
            double hc = plieuse?.DoigtContact > 0 ? plieuse.DoigtContact : 10;
            double ht = plieuse?.DoigtHauteur > 0 ? plieuse.DoigtHauteur : 35;
            if (ht < hc + 4) ht = hc + 4;
            const double postW = 8, footLen = 26, footH = 6;

            // corps du doigt en L (métal) : face d'appui à gauche (vers la tôle),
            // pied + corps vers l'arrière (droite).
            var corps = new[]
            {
                T(bd, ht), T(bd + postW, ht), T(bd + postW, footH),
                T(bd + footLen, footH), T(bd + footLen, 0), T(bd, 0)
            };
            using (var b = new SolidBrush(Theme.Outil))
            using (var pnC = new Pen(Color.FromArgb(138, 146, 158), 1.3f) { LineJoin = LineJoin.Round })
            {
                g.FillPolygon(b, corps);
                g.DrawPolygon(pnC, corps);
            }

            // axe rond en tête
            var rod = T(bd + postW / 2, ht);
            float rr = (float)(4 * sc);
            using (var b = new SolidBrush(Theme.Matrice))
            using (var pnR = new Pen(Color.FromArgb(138, 146, 158), 1.2f))
            {
                g.FillEllipse(b, rod.X - rr, rod.Y - rr, 2 * rr, 2 * rr);
                g.DrawEllipse(pnR, rod.X - rr, rod.Y - rr, 2 * rr, 2 * rr);
            }

            // face d'appui tôle (0 → contact) : bande orange sur le bord avant
            using var pn = new Pen(Theme.Accent, 3.5f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pn, T(bd, 0), T(bd, hc));
        }

        // Sigles d'etape : memes couleurs qu'au pupitre, pour ne jamais confondre les deux.
        static readonly Color CBlue  = Color.FromArgb(111, 208, 255);   // ⇄ a plat
        static readonly Color CAmber = Color.FromArgb(255, 184, 77);    // ⇅ dessus/dessous

        /// <summary>En dessous, l'operateur tient trop court : les mains arrivent pres du bec.</summary>
        public const double PriseAlerte = 50;

        /// <summary>Epaisseur dessinee minimale de la tole, en pixels ecran.</summary>
        const double EpaisseurMiniPx = 5.0;

        /// <summary>
        /// Sigles de retournement + alerte securite, en haut a droite de la vue.
        ///   ⇄ BLEU  = retournement A PLAT (bout pour bout) : la face ne change pas,
        ///             la butee lit le pan aval.
        ///   ⇅ AMBRE = retournement DESSUS/DESSOUS : la face laquee change de cote,
        ///             les plis deja formes pointent a l'oppose.
        /// Deux couleurs franchement differentes : d'un coup d'oeil on sait lequel c'est.
        /// </summary>
        void DessinerSigles(Graphics g)
        {
            if (etat?.Op == null || piece == null) return;

            float x = Math.Max(12, Width - 252), y = 14;
            using var f = new Font("Segoe UI", 8.5f);

            // FACE DESSUS. Meme code couleur que la tole : bleu = FNL, violet = FL.
            Color cf = etat.Op.Retournee ? Theme.ToleFL : Theme.Tole;
            using (var b = new SolidBrush(cf))
            {
                g.FillRectangle(b, x + 4, y + 4, 12, 12);
                g.DrawString(etat.Op.Retournee ? "FL dessus — laquage visible"
                                               : "FNL dessus — laquage protégé dessous", f, b, x + 26, y + 3);
            }
            y += 24;

            // PLI A LA BUTEE. Le pan lu porte un retour deja forme : c'est LUI qui vient contre
            // le doigt, pas le bord brut. On pousse, on ne retourne rien — d'ou une simple
            // fleche vers la butee, et surtout pas le sigle du retournement.
            int appui = piece.PliAppui(etat.Etape);
            if (appui >= 0)
            {
                SigleAppui(g, x + 10, y + 10, Theme.Accent);
                using var b = new SolidBrush(Theme.Accent);
                g.DrawString($"appui sur le retour du pli {appui + 1}", f, b, x + 26, y + 3);
                y += 24;
            }

            // Un retournement est un CHANGEMENT entre deux etapes, pas un etat de l'etape.
            // ButeeAval dit seulement quel bout part a la butee. Si l'etape d'avant avait deja
            // ce bout-la, on ne manipule RIEN : on pousse, et le pli deja forme vient a l'appui.
            // Aucun sigle. Idem pour la face. Et a la premiere etape il n'y a rien avant :
            // c'est le depart du pliage, on presente la tole comme on veut — pas de sigle.
            Operation prec = (etat.Etape > 0 && etat.Etape - 1 < piece.Sequence.Count)
                             ? piece.Sequence[etat.Etape - 1] : null;
            bool aPlat = prec != null && etat.Op.ButeeAval != prec.ButeeAval;
            bool face  = prec != null && etat.Op.Retournee != prec.Retournee;

            if (aPlat)
            {
                SigleAPlat(g, x + 10, y + 10, CBlue);
                using var b = new SolidBrush(CBlue);
                g.DrawString("retourné 180° à plat (bout pour bout)", f, b, x + 26, y + 3);
                y += 24;
            }
            if (face)
            {
                SigleFace(g, x + 10, y + 10, CAmber);
                using var b = new SolidBrush(CAmber);
                g.DrawString("retourné dessus/dessous", f, b, x + 26, y + 3);
                y += 24;
            }

            // Securite : ce que l'operateur a en main de son cote. Regle metier (Weapon) :
            // toujours le plus grand cote vers l'operateur. Sous 50 mm, les doigts sont pres
            // du poincon — surtout avec un interimaire ou un apprenti pas encore forme.
            double prise = Solveur.PriseOperateur(piece.Segments, etat.Op.Bend, etat.Op.ButeeAval);
            if (prise > 0 && prise < PriseAlerte)
            {
                MainDanger(g, x + 10, y + 11, 9f);
                using var b = new SolidBrush(Theme.Alerte);
                g.DrawString($"prise {prise:0} mm — attention aux doigts", f, b, x + 26, y + 3);
            }
        }

        /// <summary>Fleche « pli a la butee » : le retour deja forme vient contre le doigt.</summary>
        static void SigleAppui(Graphics g, float cx, float cy, Color c)
        {
            using var pn = new Pen(c, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            Fleche(g, pn, cx - 9, cy, cx + 3, cy);
            g.DrawLine(pn, cx + 7, cy - 6, cx + 7, cy + 6);   // le doigt de butee
        }

        /// <summary>⇄ : deux fleches horizontales opposees.</summary>
        static void SigleAPlat(Graphics g, float cx, float cy, Color c)
        {
            using var pn = new Pen(c, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            Fleche(g, pn, cx - 8, cy - 4, cx + 8, cy - 4);
            Fleche(g, pn, cx + 8, cy + 4, cx - 8, cy + 4);
        }

        /// <summary>⇅ : deux fleches verticales opposees.</summary>
        static void SigleFace(Graphics g, float cx, float cy, Color c)
        {
            using var pn = new Pen(c, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            Fleche(g, pn, cx - 4, cy + 8, cx - 4, cy - 8);
            Fleche(g, pn, cx + 4, cy - 8, cx + 4, cy + 8);
        }

        static void Fleche(Graphics g, Pen pn, float x1, float y1, float x2, float y2)
        {
            g.DrawLine(pn, x1, y1, x2, y2);
            double a = Math.Atan2(y2 - y1, x2 - x1);
            const double t = Math.PI / 6, L = 4.5;
            g.DrawLine(pn, x2, y2, (float)(x2 - L * Math.Cos(a - t)), (float)(y2 - L * Math.Sin(a - t)));
            g.DrawLine(pn, x2, y2, (float)(x2 - L * Math.Cos(a + t)), (float)(y2 - L * Math.Sin(a + t)));
        }

        /// <summary>Main rouge dans un rond : paume, quatre doigts, pouce. Attention aux doigts.</summary>
        static void MainDanger(Graphics g, float cx, float cy, float r)
        {
            using var pn = new Pen(Theme.Alerte, 1.6f);
            g.DrawEllipse(pn, cx - r, cy - r, 2 * r, 2 * r);

            using var pm = new Pen(Theme.Alerte, 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float u = r / 4f;
            g.DrawRectangle(pm, cx - 1.6f * u, cy + 0.2f * u, 3.2f * u, 2.0f * u);   // paume
            for (int i = 0; i < 4; i++)                                              // doigts
            {
                float fx = cx - 1.15f * u + i * 0.77f * u;
                float h = (i == 0 || i == 3) ? 1.4f * u : 1.9f * u;
                g.DrawLine(pm, fx, cy + 0.2f * u, fx, cy + 0.2f * u - h);
            }
            g.DrawLine(pm, cx - 1.6f * u, cy + 0.9f * u, cx - 2.7f * u, cy - 0.2f * u); // pouce
        }

        void Legende(Graphics g)
        {
            using var f = new Font("Segoe UI", 8.5f);
            g.DrawString("bleu = FNL dessus · violet = FL dessus · vert = reprise · rouge = collision",
                f, new SolidBrush(Theme.Discret), 12, Height - 22);
        }

        void Centre(Graphics g, string t)
        {
            using var f = new Font("Segoe UI", 11);
            var sz = g.MeasureString(t, f);
            g.DrawString(t, f, new SolidBrush(Theme.Discret), (Width - sz.Width) / 2, (Height - sz.Height) / 2);
        }
    }
}
