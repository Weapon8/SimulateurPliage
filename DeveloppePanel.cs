using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace SimulateurPliage
{
    // Vue "developpe" : la tole a plat (somme des pans), lignes de pli a leur position.
    // Face de reference BLEUE ; quand un pli oblige a RETOURNER la piece -> ROUGE (⟲).
    public class DeveloppePanel : Panel
    {
        Piece piece;
        int cur;
        bool[] flips;   // par operation de la sequence : true = retournement necessaire

        static readonly Color CBg    = Color.FromArgb(20, 24, 31);
        static readonly Color CFace  = Color.FromArgb(63, 131, 235);   // face de reference (bleu)
        static readonly Color CFlip  = Color.FromArgb(229, 83, 75);    // retournement (rouge)
        static readonly Color CEdge  = Color.FromArgb(150, 160, 172);
        static readonly Color CMuted = Color.FromArgb(138, 149, 162);
        static readonly Color CAccent= Color.FromArgb(255, 122, 26);
        static readonly Color CGrid  = Color.FromArgb(34, 40, 49);
        static readonly Color CTxt   = Color.FromArgb(210, 218, 228);

        public DeveloppePanel() { DoubleBuffered = true; BackColor = CBg; }

        public void SetData(Piece p, int step, bool[] retournements)
        { piece = p; cur = step; flips = retournements; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(CBg);
            if (piece == null || piece.Segments.Count == 0) { Center(g, "Ajoute des pans"); return; }

            int nSeg = piece.Segments.Count;
            double total = 0; foreach (var s in piece.Segments) total += s;
            if (total <= 0) { Center(g, "Longueurs de pans nulles"); return; }

            // positions cumulees des lignes de pli (entre pan i et i+1)
            var cum = new double[nSeg + 1];
            for (int i = 0; i < nSeg; i++) cum[i + 1] = cum[i] + piece.Segments[i];

            // etat de retournement PAR PLI (une ligne de pli = un bend)
            int nb = piece.NbPlis;
            var bendFlip = new bool[Math.Max(0, nb)];
            var bendActive = -1;
            if (piece.Sequence != null)
            {
                for (int i = 0; i < piece.Sequence.Count; i++)
                {
                    int b = piece.Sequence[i].Bend;
                    if (b >= 0 && b < nb && flips != null && i < flips.Length && flips[i]) bendFlip[b] = true;
                }
                if (cur >= 0 && cur < piece.Sequence.Count) bendActive = piece.Sequence[cur].Bend;
            }
            bool stepFlip = flips != null && cur >= 0 && cur < flips.Length && flips[cur];

            // --- cadrage ---
            int mL = 40, mR = 40, top = 78;
            double sc = (Width - mL - mR) / total;
            if (sc <= 0) sc = 1;
            int bandH = Math.Min(120, Math.Max(60, Height - top - 150));
            int y0 = top, y1 = top + bandH;
            float X(double mm) => (float)(mL + mm * sc);

            // --- titre + resume ---
            using (var ft = new Font("Segoe UI", 11, FontStyle.Bold))
                g.DrawString("DÉVELOPPÉ — tôle à plat", ft, new SolidBrush(CAccent), mL, 14);
            int nFlips = 0; foreach (var b in bendFlip) if (b) nFlips++;
            using (var f = new Font("Segoe UI", 9))
                g.DrawString($"L développé {total:0} mm   ·   {nSeg} pans   ·   {nb} plis   ·   {nFlips} retournement(s)",
                    f, new SolidBrush(CMuted), mL, 40);

            // --- bande (la tole a plat) : face bleue, rouge si l'etape courante est un retournement ---
            var faceCol = stepFlip ? CFlip : CFace;
            using (var b = new SolidBrush(Color.FromArgb(48, faceCol)))
                g.FillRectangle(b, X(0), y0, X(total) - X(0), bandH);
            using (var pn = new Pen(faceCol, 2f))
                g.DrawRectangle(pn, X(0), y0, X(total) - X(0), bandH);

            // --- cotes de pans (longueurs) ---
            using (var f = new Font("Consolas", 9))
                for (int i = 0; i < nSeg; i++)
                {
                    float xm = X((cum[i] + cum[i + 1]) / 2);
                    string t = piece.Segments[i].ToString("0.#", CultureInfo.InvariantCulture);
                    var sz = g.MeasureString(t, f);
                    g.DrawString(t, f, new SolidBrush(CTxt), xm - sz.Width / 2, y1 + 8);
                    // fleche de cote
                    using var pn = new Pen(CMuted, 1f);
                    g.DrawLine(pn, X(cum[i]) + 2, y1 + 20, X(cum[i + 1]) - 2, y1 + 20);
                }

            // --- lignes de pli ---
            for (int b = 0; b < nb; b++)
            {
                float x = X(cum[b + 1]);
                bool fl = bendFlip[b];
                bool act = b == bendActive;
                Color lc = act ? CAccent : (fl ? CFlip : CFace);
                float w = act ? 3f : 2f;
                using (var pn = new Pen(lc, w) { DashStyle = fl ? DashStyle.Dash : DashStyle.Solid })
                    g.DrawLine(pn, x, y0 - 10, x, y1 + 10);

                // etiquette du pli
                var op = FindOp(b);
                string lab = "P" + (b + 1);
                string sub = op != null ? $"{op.AngleCible:0}° {(op.Sens == Sens.Haut ? "H" : "B")}" : "—";
                using var f1 = new Font("Consolas", 9, FontStyle.Bold);
                using var f2 = new Font("Consolas", 8);
                var s1 = g.MeasureString(lab, f1);
                g.DrawString(lab, f1, new SolidBrush(act ? CAccent : CTxt), x - s1.Width / 2, y0 - 32);
                var s2 = g.MeasureString(sub, f2);
                g.DrawString(sub, f2, new SolidBrush(fl ? CFlip : CMuted), x - s2.Width / 2, y0 - 18);

                if (fl)
                {
                    using var fr = new Font("Segoe UI", 8, FontStyle.Bold);
                    g.DrawString("⟲", fr, new SolidBrush(CFlip), x - 6, y1 + 34);
                }
            }

            // --- banniere retournement pour l'etape courante ---
            if (stepFlip)
            {
                using var fb = new Font("Segoe UI", 11, FontStyle.Bold);
                string t = "⟲  RETOURNER LA PIÈCE avant ce pli";
                var sz = g.MeasureString(t, fb);
                var r = new RectangleF((Width - sz.Width) / 2 - 12, Height - 54, sz.Width + 24, 34);
                using (var b = new SolidBrush(Color.FromArgb(40, CFlip))) g.FillRectangle(b, r);
                using (var pn = new Pen(CFlip, 1.4f)) g.DrawRectangle(pn, r.X, r.Y, r.Width, r.Height);
                g.DrawString(t, fb, new SolidBrush(CFlip), r.X + 12, r.Y + 7);
            }

            // --- legende ---
            using (var f = new Font("Segoe UI", 8.5f))
                g.DrawString("bleu = face de référence   ·   rouge pointillé = pli en retournement   ·   orange = pli actif",
                    f, new SolidBrush(CMuted), mL, Height - 22);
        }

        Operation FindOp(int bend)
        {
            if (piece?.Sequence == null) return null;
            Operation last = null;
            foreach (var o in piece.Sequence) if (o.Bend == bend) last = o;
            return last;
        }

        void Center(Graphics g, string t)
        {
            using var f = new Font("Segoe UI", 11);
            var sz = g.MeasureString(t, f);
            g.DrawString(t, f, new SolidBrush(CMuted), (Width - sz.Width) / 2, (Height - sz.Height) / 2);
        }
    }
}
