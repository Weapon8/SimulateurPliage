using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using SimulateurPliage.Pliage;

namespace SimulateurPliage.Vues
{
    /// <summary>
    /// Vue développé : la tôle à plat, lignes de pli à leur position.
    /// Face de référence en bleu ; un pli qui impose un retournement passe en rouge.
    /// </summary>
    public class VueDeveloppe : Panel
    {
        Piece piece;
        int etape;
        bool[] flips;

        public VueDeveloppe()
        {
            DoubleBuffered = true;
            BackColor = Theme.Fond;
        }

        public void Afficher(Piece p, int step)
        {
            piece = p;
            etape = step;
            flips = p?.Retournements();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Fond);

            if (piece == null || piece.Segments.Count == 0) { Centre(g, "Ajoute des pans"); return; }
            double total = piece.Developpe;
            if (total <= 0) { Centre(g, "Longueurs de pans nulles"); return; }

            int nSeg = piece.Segments.Count, nb = piece.NbPlis;

            var cum = new double[nSeg + 1];
            for (int i = 0; i < nSeg; i++) cum[i + 1] = cum[i] + piece.Segments[i];

            var retourne = new bool[Math.Max(0, nb)];
            int actif = -1;
            for (int i = 0; i < piece.Sequence.Count; i++)
            {
                int b = piece.Sequence[i].Bend;
                if (b >= 0 && b < nb && flips != null && i < flips.Length && flips[i]) retourne[b] = true;
            }
            if (etape >= 0 && etape < piece.Sequence.Count) actif = piece.Sequence[etape].Bend;
            bool flipCourant = flips != null && etape >= 0 && etape < flips.Length && flips[etape];

            const int mL = 40, mR = 40, top = 78;
            double sc = (Width - mL - mR) / total;
            if (sc <= 0) sc = 1;
            int bandH = Math.Min(120, Math.Max(60, Height - top - 150));
            int y0 = top, y1 = top + bandH;
            float X(double mm) => (float)(mL + mm * sc);

            using (var ft = new Font("Segoe UI", 11, FontStyle.Bold))
                g.DrawString("DÉVELOPPÉ — tôle à plat", ft, new SolidBrush(Theme.Accent), mL, 14);

            int nFlips = 0; foreach (var b in retourne) if (b) nFlips++;
            using (var f = new Font("Segoe UI", 9))
                g.DrawString($"L développé {total:0} mm   ·   {nSeg} pans   ·   {nb} plis   ·   {nFlips} retournement(s)",
                    f, new SolidBrush(Theme.Discret), mL, 40);

            Color face = flipCourant ? Theme.Alerte : Theme.Tole;
            using (var b = new SolidBrush(Color.FromArgb(48, face)))
                g.FillRectangle(b, X(0), y0, X(total) - X(0), bandH);
            using (var pn = new Pen(face, 2f))
                g.DrawRectangle(pn, X(0), y0, X(total) - X(0), bandH);

            using (var f = new Font("Consolas", 9))
            using (var pn = new Pen(Theme.Discret, 1f))
                for (int i = 0; i < nSeg; i++)
                {
                    float xm = X((cum[i] + cum[i + 1]) / 2);
                    string t = piece.Segments[i].ToString("0.#", CultureInfo.InvariantCulture);
                    var sz = g.MeasureString(t, f);
                    g.DrawString(t, f, new SolidBrush(Theme.Texte), xm - sz.Width / 2, y1 + 8);
                    g.DrawLine(pn, X(cum[i]) + 2, y1 + 20, X(cum[i + 1]) - 2, y1 + 20);
                }

            for (int b = 0; b < nb; b++)
            {
                float x = X(cum[b + 1]);
                bool fl = retourne[b], act = b == actif;
                Color lc = act ? Theme.Accent : (fl ? Theme.Alerte : Theme.Tole);

                using (var pn = new Pen(lc, act ? 3f : 2f) { DashStyle = fl ? DashStyle.Dash : DashStyle.Solid })
                    g.DrawLine(pn, x, y0 - 10, x, y1 + 10);

                var op = DernierOp(b);
                string lab = "P" + (b + 1);
                string sub = op != null ? $"{op.AngleCible:0}° {(op.Sens == Sens.Haut ? "H" : "B")}" : "—";

                using var f1 = new Font("Consolas", 9, FontStyle.Bold);
                using var f2 = new Font("Consolas", 8);
                var s1 = g.MeasureString(lab, f1);
                g.DrawString(lab, f1, new SolidBrush(act ? Theme.Accent : Theme.Texte), x - s1.Width / 2, y0 - 32);
                var s2 = g.MeasureString(sub, f2);
                g.DrawString(sub, f2, new SolidBrush(fl ? Theme.Alerte : Theme.Discret), x - s2.Width / 2, y0 - 18);

                if (fl)
                    using (var fr = new Font("Segoe UI", 8, FontStyle.Bold))
                        g.DrawString("⟲", fr, new SolidBrush(Theme.Alerte), x - 6, y1 + 34);
            }

            if (flipCourant)
            {
                using var fb = new Font("Segoe UI", 11, FontStyle.Bold);
                string t = "⟲  RETOURNER LA PIÈCE avant ce pli";
                var sz = g.MeasureString(t, fb);
                var r = new RectangleF((Width - sz.Width) / 2 - 12, Height - 54, sz.Width + 24, 34);
                using (var b = new SolidBrush(Color.FromArgb(40, Theme.Alerte))) g.FillRectangle(b, r);
                using (var pn = new Pen(Theme.Alerte, 1.4f)) g.DrawRectangle(pn, r.X, r.Y, r.Width, r.Height);
                g.DrawString(t, fb, new SolidBrush(Theme.Alerte), r.X + 12, r.Y + 7);
            }

            using (var f = new Font("Segoe UI", 8.5f))
                g.DrawString("bleu = face de référence · rouge pointillé = pli en retournement · orange = pli actif",
                    f, new SolidBrush(Theme.Discret), mL, Height - 22);
        }

        Operation DernierOp(int bend)
        {
            Operation last = null;
            if (piece?.Sequence != null)
                foreach (var o in piece.Sequence) if (o.Bend == bend) last = o;
            return last;
        }

        void Centre(Graphics g, string t)
        {
            using var f = new Font("Segoe UI", 11);
            var sz = g.MeasureString(t, f);
            g.DrawString(t, f, new SolidBrush(Theme.Discret), (Width - sz.Width) / 2, (Height - sz.Height) / 2);
        }
    }
}
