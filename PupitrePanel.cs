using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace SimulateurPliage
{
    // Vue "pupitre" facon commande numerique Cybelec/Delem :
    // tableau des plis avec angle, R (butee = cote interieure), L pan, V, sens.
    public class PupitrePanel : Panel
    {
        readonly MachineConfig cfg;
        Piece piece;
        int cur;
        Poincon poin;
        Matrice mat;
        Embase emb;

        static readonly Color CBg    = Color.FromArgb(12, 15, 20);
        static readonly Color CHead  = Color.FromArgb(140, 150, 162);
        static readonly Color CGreen = Color.FromArgb(80, 230, 120);   // afficheur
        static readonly Color COrange= Color.FromArgb(255, 170, 60);
        static readonly Color CRow   = Color.FromArgb(22, 27, 34);
        static readonly Color CCurBg = Color.FromArgb(40, 30, 12);
        static readonly Color CRed   = Color.FromArgb(235, 90, 80);
        static readonly Color CLine  = Color.FromArgb(40, 46, 55);

        public PupitrePanel(MachineConfig c) { cfg = c; DoubleBuffered = true; BackColor = CBg; }

        public void SetData(Piece p, int step, Poincon pn, Matrice mt, Embase eb)
        { piece = p; cur = step; poin = pn; mat = mt; emb = eb; Invalidate(); }

        // colonnes : x de depart (proportion de la largeur)
        static readonly (string h, float x, bool num)[] Cols =
        {
            ("N°",    0.03f, false),
            ("PLI",   0.12f, false),
            ("ANGLE", 0.24f, true),
            ("R butée (int.)", 0.42f, true),
            ("L pan", 0.64f, true),
            ("V",     0.80f, true),
            ("SENS",  0.89f, false),
        };

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(CBg);
            if (piece == null || piece.Sequence.Count == 0) { Msg(g, "Aucune opération"); return; }

            using var fHead = new Font("Consolas", 10.5f, FontStyle.Bold);
            using var fCell = new Font("Consolas", 15f, FontStyle.Bold);
            using var fSmall= new Font("Consolas", 10f, FontStyle.Bold);

            int W = Width, pad = 14;
            int top = 14, rowH = 42, headH = 30;

            // titre
            using (var ft = new Font("Segoe UI", 11, FontStyle.Bold))
                g.DrawString("PUPITRE — séquence de pliage", ft, new SolidBrush(COrange), pad, top);
            top += 30;

            // entete
            foreach (var c in Cols)
                g.DrawString(c.h, fHead, new SolidBrush(CHead), c.x * W, top);
            top += headH;
            using (var pn = new Pen(CLine, 1.4f)) g.DrawLine(pn, pad, top - 4, W - pad, top - 4);

            for (int i = 0; i < piece.Sequence.Count; i++)
            {
                var o = piece.Sequence[i];
                var st = FoldEngine.Build(piece, i, cfg, poin, mat, emb);
                bool hit = st.Collisions.Count > 0;
                bool active = i == cur;

                int ry = top + i * rowH;
                var rowRect = new Rectangle(pad, ry, W - 2 * pad, rowH - 6);
                using (var b = new SolidBrush(active ? CCurBg : CRow)) g.FillRectangle(b, rowRect);
                if (active) using (var pn = new Pen(COrange, 1.6f)) g.DrawRectangle(pn, rowRect);

                Color val = hit ? CRed : (active ? COrange : CGreen);

                double from = AngleBefore(i);
                double rInt = ButeeInterieure(o.Bend);
                double lPan = (o.Bend + 1 < piece.Segments.Count) ? piece.Segments[o.Bend + 1] : 0;

                float cy = ry + 8;
                Cell(g, fCell, val, Cols[0].x * W, cy, (i + 1).ToString("00"));
                Cell(g, fCell, val, Cols[1].x * W, cy, "P" + (o.Bend + 1));
                Cell(g, fCell, val, Cols[2].x * W, cy, o.AngleCible.ToString("0", CultureInfo.InvariantCulture) + "\u00b0");
                Cell(g, fCell, val, Cols[3].x * W, cy, rInt.ToString("0.0", CultureInfo.InvariantCulture));
                Cell(g, fCell, val, Cols[4].x * W, cy, lPan.ToString("0.0", CultureInfo.InvariantCulture));
                Cell(g, fCell, val, Cols[5].x * W, cy, ((int)o.V).ToString());
                Cell(g, fSmall, val, Cols[6].x * W, cy + 3, (o.Sens == Sens.Haut ? "HAUT" : "BAS") + (o.Reprise ? " *" : ""));

                // 2e ligne info : from->to + etat
                string sub = $"{from:0}\u00b0\u2192{o.AngleCible:0}\u00b0" + (o.Reprise ? "  reprise" : "  direct") + (hit ? "   ! " + st.Collisions[0].Type : "");
                g.DrawString(sub, fSmall, new SolidBrush(hit ? CRed : CHead), Cols[2].x * W, ry + 24);
            }

            // pied : rappel cotes
            using var fp = new Font("Consolas", 9.5f);
            string ep = piece != null ? piece.Epaisseur.ToString("0.##", CultureInfo.InvariantCulture) : "?";
            string mode = (piece != null && piece.CotesExterieures) ? "extérieures (R converti en int.)" : "intérieures";
            g.DrawString($"ép {ep} mm  ·  saisie {mode}  ·  R = cote intérieure lue à la butée arrière",
                fp, new SolidBrush(CHead), pad, Height - 24);
        }

        void Cell(Graphics g, Font f, Color c, float x, float y, string t)
            => g.DrawString(t, f, new SolidBrush(c), x, y);

        double AngleBefore(int s)
        {
            if (piece == null || s < 0 || s >= piece.Sequence.Count) return 180;
            int bend = piece.Sequence[s].Bend;
            double a = 180;
            for (int i = 0; i < s; i++) if (piece.Sequence[i].Bend == bend) a = piece.Sequence[i].AngleCible;
            return a;
        }

        // R = cote INTERIEURE du pan cote butee jusqu'a la ligne de pli.
        // Butees interieures : si saisie en exterieures, on retranche l'epaisseur au pli.
        double ButeeInterieure(int bend)
        {
            if (piece == null || bend < 0 || bend >= piece.Segments.Count) return 0;
            double L = piece.Segments[bend];
            if (piece.CotesExterieures) L -= piece.Epaisseur;   // ext -> int au pli
            return Math.Max(0, L);
        }

        void Msg(Graphics g, string t)
        {
            using var f = new Font("Segoe UI", 11);
            var sz = g.MeasureString(t, f);
            g.DrawString(t, f, new SolidBrush(CHead), (Width - sz.Width) / 2, (Height - sz.Height) / 2);
        }
    }
}
