using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace SimulateurPliage
{
    public class MainForm : Form
    {
        // palette TolTem
        static readonly Color CBack   = Color.FromArgb(20, 24, 31);
        static readonly Color CPanel  = Color.FromArgb(27, 32, 39);
        static readonly Color CInput  = Color.FromArgb(38, 44, 53);
        static readonly Color CText   = Color.FromArgb(230, 235, 240);
        static readonly Color CMuted  = Color.FromArgb(138, 149, 162);
        static readonly Color CAccent = Color.FromArgb(255, 122, 26);
        static readonly Color CBtn    = Color.FromArgb(43, 49, 59);
        static readonly Color CInk    = Color.FromArgb(18, 21, 27);
        static readonly Color CGrey   = Color.FromArgb(70, 78, 90);
        static readonly Color CSep    = Color.FromArgb(46, 53, 63);
        static readonly Color CBleu   = Color.FromArgb(63, 131, 235);   // pli direct
        static readonly Color CVert   = Color.FromArgb(63, 185, 80);    // pli avec reprise
        static readonly Color CRouge  = Color.FromArgb(229, 83, 75);    // collision
        static readonly Color CTool   = Color.FromArgb(120, 128, 140);  // outils
        static readonly Color CDie    = Color.FromArgb(90, 98, 110);

        readonly MachineConfig cfg = new();
        ToolLib lib;
        Poincon curPoin;
        Matrice curMat;
        Piece piece = Piece.Demo();
        int step = 0;
        bool _load;

        NumericUpDown nNb, nEp;
        ComboBox cbCotes, cbPoin, cbMat;
        DataGridView dgSeg, dgSeq;
        SectionPanel view;
        PupitrePanel pupitre;
        DeveloppePanel developpe;
        TrackBar tb;
        Label lblStep, lblAlert;
        RichTextBox rtSeq;
        Panel right;

        const int PANW = 300;   // largeur utile du panneau gauche

        public MainForm()
        {
            Text = "Simulateur de pliage — collisions outillage · TolTem   [v0.8]";
            Width = 1320; Height = 860; StartPosition = FormStartPosition.CenterScreen;
            BackColor = CBack; ForeColor = CText; Font = new Font("Segoe UI", 9);
            lib = ToolLib.Load();
            curPoin = lib.Poincons[0];
            curMat = lib.Matrices.Find(m => m.Nom.Contains("2045")) ?? lib.Matrices[0];
            SyncCfgFromTools();
            BuildUi();
            view.SetTools(curMat, curPoin, cfg.Embase);
            ReloadGridsFromPiece();
            Recompute();
        }

        void SyncCfgFromTools()
        {
            if (curPoin != null)
            {
                cfg.PoinconHauteur = curPoin.Hauteur; cfg.PoinconAngleDeg = curPoin.AngleDeg;
                cfg.CorpsLg = curPoin.CorpsLg;
                // ColRetrait/ColHauteur retires : le col de cygne est desormais dans le contour du poincon.
            }
            if (curMat != null) cfg.BlocLargeur = curMat.BlocLargeur;
        }

        // ------------------------------------------------ UI ----------
        void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = CGrey };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 342));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var left = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = CPanel, Padding = new Padding(14, 10, 14, 20), Margin = new Padding(0) };
            right = new Panel { Dock = DockStyle.Fill, BackColor = CBack, Margin = new Padding(0) };
            root.Controls.Add(left, 0, 0);
            root.Controls.Add(right, 1, 0);
            int y = 4;

            y = Title(left, "OUTILLAGE", y, false);
            cbPoin = Combo(left, "Poinçon", PoinNames(), 0, ref y, i => { curPoin = lib.Poincons[i]; SyncCfgFromTools(); view.SetTools(curMat, curPoin, cfg.Embase); Recompute(); });
            cbMat = Combo(left, "Matrice", MatNames(), Math.Max(0, lib.Matrices.IndexOf(curMat)), ref y, i => { curMat = lib.Matrices[i]; SyncCfgFromTools(); view.SetTools(curMat, curPoin, cfg.Embase); RebuildVColumn(); Recompute(); });

            y = Title(left, "PIÈCE", y);
            nNb = Num(left, "Nombre de plis", 2, 0, 12, 1, 0, ref y, v => { SetNbPlis((int)v); });
            nEp = Num(left, "Épaisseur (mm)", 1.0, 0.4, 5, 0.1, 2, ref y, v => { piece.Epaisseur = v; Recompute(); });
            cbCotes = Combo(left, "Cotes", new[] { "intérieures", "extérieures" }, 0, ref y, i => { piece.CotesExterieures = i == 1; Recompute(); });

            y = Title(left, "PANS (longueurs mm)", y);
            dgSeg = Grid(left, 96, ref y);
            dgSeg.Columns.Add(TextCol("pan", "Pan", 56, true));
            dgSeg.Columns.Add(TextCol("lg", "Longueur", 150, false));
            dgSeg.CellEndEdit += (s, e) => { if (!_load) { ReadSeg(); Recompute(); } };

            y = Title(left, "SÉQUENCE DE PLIAGE", y);
            dgSeq = Grid(left, 240, ref y);          // agrandie : c'est le coeur de l'outil
            dgSeq.RowTemplate.Height = 28;
            dgSeq.Columns.Add(TextCol("ord", "N°", 30, true));
            dgSeq.Columns.Add(TextCol("pli", "Pli", 38, false));
            dgSeq.Columns.Add(TextCol("ang", "Angle°", 54, false));
            dgSeq.Columns.Add(ComboCol("sens", "Sens", new[] { "Haut", "Bas" }, 58));
            dgSeq.Columns.Add(ComboCol("v", "V", VStrings(), 46));
            var cRep = new DataGridViewCheckBoxColumn { Name = "rep", HeaderText = "Reprise", Width = 58 };
            dgSeq.Columns.Add(cRep);
            dgSeq.CellEndEdit += (s, e) => { if (!_load) { ReadSeq(); Recompute(); } };
            dgSeq.CurrentCellDirtyStateChanged += (s, e) => { if (dgSeq.IsCurrentCellDirty) dgSeq.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            dgSeq.DataError += (s, e) => { e.ThrowException = false; };

            var barSeq = new FlowLayoutPanel { Left = 12, Top = y, Width = PANW + 10, Height = 34, BackColor = CPanel };
            left.Controls.Add(barSeq); y += 42;
            barSeq.Controls.Add(Btn("+ étape", 70, () => { AddOp(); }));
            barSeq.Controls.Add(Btn("–", 34, () => { DelOp(); }));
            barSeq.Controls.Add(Btn("↑", 34, () => { MoveOp(-1); }));
            barSeq.Controls.Add(Btn("↓", 34, () => { MoveOp(+1); }));
            barSeq.Controls.Add(Btn("Exemple U", 90, () => { piece = Piece.Demo(); ReloadGridsFromPiece(); step = 0; Recompute(); }));

            y = Title(left, "RÉGLAGES MACHINE  (À MESURER)", y);

            y = SubTitle(left, "Poinçon", y);
            MNum(left, "Hauteur", cfg.PoinconHauteur, ref y, v => { cfg.PoinconHauteur = v; if (curPoin != null) curPoin.Hauteur = v; });
            MNum(left, "Angle pointe (°)", cfg.PoinconAngleDeg, ref y, v => cfg.PoinconAngleDeg = v);
            MNum(left, "Largeur pointe", cfg.PoinconPointeLg, ref y, v => cfg.PoinconPointeLg = v);

            y = SubTitle(left, "Butée & tablier", y);
            MNum(left, "Tablier déport", cfg.TablierDeport, ref y, v => cfg.TablierDeport = v);
            MNum(left, "Hauteur libre ouverte", cfg.HauteurLibre, ref y, v => cfg.HauteurLibre = v);
            MNum(left, "Butée arrière max", cfg.ButeeMax, ref y, v => cfg.ButeeMax = v);

            y = SubTitle(left, "Embases", y);
            MNum(left, "Porte-poinçon hauteur", cfg.Embase.PortePoinconH, ref y, v => cfg.Embase.PortePoinconH = v);
            MNum(left, "Porte-poinçon largeur", cfg.Embase.PortePoinconLg, ref y, v => cfg.Embase.PortePoinconLg = v);
            MNum(left, "Semelle hauteur", cfg.Embase.SemelleH, ref y, v => cfg.Embase.SemelleH = v);
            MNum(left, "Semelle largeur", cfg.Embase.SemelleLg, ref y, v => cfg.Embase.SemelleLg = v);

            // ---------------- droite : vue + controles ----------------
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 190, BackColor = CPanel };
            right.Controls.Add(bottom);
            lblAlert = new Label { Dock = DockStyle.Top, Height = 30, ForeColor = CText, BackColor = CPanel,
                Padding = new Padding(10, 6, 10, 0), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            bottom.Controls.Add(lblAlert);
            rtSeq = new RichTextBox { Dock = DockStyle.Fill, BackColor = CInput, ForeColor = CText, BorderStyle = BorderStyle.None,
                ReadOnly = true, Font = new Font("Consolas", 9.5f) };
            bottom.Controls.Add(rtSeq);
            rtSeq.BringToFront();

            var ctrl = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = CBack };
            right.Controls.Add(ctrl);
            var bPrev = Btn("◀", 44, () => { SetStep(step - 1); }); bPrev.Left = 10; bPrev.Top = 9; bPrev.Height = 34; ctrl.Controls.Add(bPrev);
            var bNext = Btn("▶", 44, () => { SetStep(step + 1); }); bNext.Left = 58; bNext.Top = 9; bNext.Height = 34; ctrl.Controls.Add(bNext);
            tb = new TrackBar { Left = 110, Top = 8, Width = 215, Minimum = 0, Maximum = 1, TickStyle = TickStyle.None, BackColor = CBack };
            tb.ValueChanged += (s, e) => { if (!_load) SetStep(tb.Value); };
            ctrl.Controls.Add(tb);
            lblStep = new Label { Left = 335, Top = 14, Width = 420, ForeColor = CAccent, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            ctrl.Controls.Add(lblStep);

            var vbar = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 292, BackColor = CBack, Padding = new Padding(0, 9, 8, 0) };
            ctrl.Controls.Add(vbar);
            var bSec = Btn("Section", 84, () => ShowView(0)); bSec.Height = 34;
            var bDev = Btn("Développé", 92, () => ShowView(2)); bDev.Height = 34;
            var bPup = Btn("Pupitre", 84, () => ShowView(1)); bPup.Height = 34;
            vbar.Controls.Add(bSec); vbar.Controls.Add(bDev); vbar.Controls.Add(bPup);

            view = new SectionPanel(cfg) { Dock = DockStyle.Fill, BackColor = CBack };
            right.Controls.Add(view);
            pupitre = new PupitrePanel(cfg) { Dock = DockStyle.Fill, Visible = false };
            right.Controls.Add(pupitre);
            developpe = new DeveloppePanel { Dock = DockStyle.Fill, Visible = false };
            right.Controls.Add(developpe);
            view.BringToFront();
        }

        void ShowView(int mode)
        {
            view.Visible = mode == 0;
            pupitre.Visible = mode == 1;
            developpe.Visible = mode == 2;
            if (mode == 0) view.BringToFront();
            else if (mode == 1) pupitre.BringToFront();
            else developpe.BringToFront();
        }

        // Retournement par operation : le 1er pli fixe la face de reference ;
        // des qu'un pli change de sens vs l'orientation courante -> retournement, et l'orientation bascule.
        bool[] Retournements()
        {
            var f = new bool[piece.Sequence.Count];
            if (piece.Sequence.Count == 0) return f;
            Sens nat = piece.Sequence[0].Sens;
            for (int i = 0; i < piece.Sequence.Count; i++)
            {
                if (i == 0) { f[i] = false; nat = piece.Sequence[0].Sens; continue; }
                if (piece.Sequence[i].Sens == nat) f[i] = false;
                else { f[i] = true; nat = piece.Sequence[i].Sens; }
            }
            return f;
        }

        // ---- helpers UI ----
        int Title(Panel p, string t, int y, bool sep = true)
        {
            if (sep)
            {
                var d = new Panel { Left = 10, Top = y + 6, Width = PANW + 10, Height = 1, BackColor = CSep };
                p.Controls.Add(d); y += 12;
            }
            var l = new Label { Text = t, Left = 10, Top = y + 6, Width = PANW, ForeColor = CAccent, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            p.Controls.Add(l); return y + 30;
        }
        int SubTitle(Panel p, string t, int y)
        {
            var l = new Label { Text = t, Left = 12, Top = y + 6, Width = PANW, ForeColor = CMuted, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            p.Controls.Add(l);
            var d = new Panel { Left = 12 + (int)(t.Length * 6.6) + 8, Top = y + 13, Width = Math.Max(10, PANW - (int)(t.Length * 6.6) - 22), Height = 1, BackColor = CSep };
            p.Controls.Add(d);
            return y + 24;
        }
        NumericUpDown Num(Panel p, string lab, double v, double min, double max, double inc, int dec, ref int y, Action<double> onCh)
        {
            var l = new Label { Text = lab, Left = 12, Top = y + 4, Width = 170, ForeColor = CText };
            var n = new NumericUpDown { Left = 190, Top = y, Width = 148, Minimum = (decimal)min, Maximum = (decimal)max,
                Increment = (decimal)inc, DecimalPlaces = dec, Value = (decimal)v, BackColor = CInput, ForeColor = CText, BorderStyle = BorderStyle.FixedSingle };
            n.ValueChanged += (s, e) => { if (!_load) onCh((double)n.Value); };
            p.Controls.Add(l); p.Controls.Add(n); y += 30; return n;
        }
        void MNum(Panel p, string lab, double v, ref int y, Action<double> onCh)
        {
            var l = new Label { Text = lab, Left = 20, Top = y + 4, Width = 168, ForeColor = CMuted };
            var n = new NumericUpDown { Left = 190, Top = y, Width = 148, Minimum = 0, Maximum = 5000, DecimalPlaces = 1,
                Increment = 1, Value = (decimal)v, BackColor = CInput, ForeColor = CText, BorderStyle = BorderStyle.FixedSingle };
            n.ValueChanged += (s, e) => { if (!_load) { onCh((double)n.Value); Recompute(); } };
            p.Controls.Add(l); p.Controls.Add(n); y += 28;
        }
        ComboBox Combo(Panel p, string lab, string[] items, int sel, ref int y, Action<int> onCh)
        {
            var l = new Label { Text = lab, Left = 12, Top = y + 4, Width = 170, ForeColor = CText };
            var c = new ComboBox { Left = 190, Top = y, Width = 148, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = CInput, ForeColor = CText, FlatStyle = FlatStyle.Flat };
            c.Items.AddRange(items); c.SelectedIndex = sel;
            c.SelectedIndexChanged += (s, e) => { if (!_load) onCh(c.SelectedIndex); };
            p.Controls.Add(l); p.Controls.Add(c); y += 30; return c;
        }
        DataGridView Grid(Panel p, int h, ref int y)
        {
            var g = new DataGridView { Left = 12, Top = y, Width = PANW + 10, Height = h, BackgroundColor = CInput,
                BorderStyle = BorderStyle.None, GridColor = CGrey, RowHeadersVisible = false, AllowUserToAddRows = false,
                AllowUserToResizeRows = false, EnableHeadersVisualStyles = false, AllowUserToResizeColumns = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect };
            g.DefaultCellStyle.BackColor = CInput; g.DefaultCellStyle.ForeColor = CText;
            g.DefaultCellStyle.SelectionBackColor = CBtn; g.DefaultCellStyle.SelectionForeColor = CText;
            g.ColumnHeadersDefaultCellStyle.BackColor = CPanel; g.ColumnHeadersDefaultCellStyle.ForeColor = CMuted;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing; g.ColumnHeadersHeight = 26;
            p.Controls.Add(g); y += h + 8; return g;
        }
        DataGridViewTextBoxColumn TextCol(string name, string head, int w, bool ro)
            => new DataGridViewTextBoxColumn { Name = name, HeaderText = head, Width = w, ReadOnly = ro };
        DataGridViewComboBoxColumn ComboCol(string name, string head, string[] items, int w)
        {
            var c = new DataGridViewComboBoxColumn { Name = name, HeaderText = head, Width = w, FlatStyle = FlatStyle.Flat };
            c.Items.AddRange(items); return c;
        }
        Button Btn(string t, int w, Action onClick)
        {
            var b = new Button { Text = t, Width = w, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = CBtn, ForeColor = CText, Margin = new Padding(2) };
            b.FlatAppearance.BorderColor = CGrey;
            b.Click += (s, e) => onClick();
            return b;
        }

        // ------------------------------------------- model <-> grids ---
        string[] PoinNames() { var a = new string[lib.Poincons.Count]; for (int i = 0; i < a.Length; i++) a[i] = lib.Poincons[i].Nom; return a; }
        string[] MatNames()  { var a = new string[lib.Matrices.Count]; for (int i = 0; i < a.Length; i++) a[i] = lib.Matrices[i].Nom; return a; }
        string[] VStrings()
        {
            var l = new List<string>();
            if (curMat != null) foreach (var vf in curMat.Vs) l.Add(((int)vf.V).ToString());
            if (l.Count == 0) l.Add("16");
            return l.ToArray();
        }
        void RebuildVColumn()
        {
            if (dgSeq == null || curMat == null) return;
            var col = dgSeq.Columns["v"] as DataGridViewComboBoxColumn;
            if (col == null) return;
            _load = true;
            col.Items.Clear();
            foreach (var s in VStrings()) col.Items.Add(s);
            string first = VStrings()[0];
            foreach (DataGridViewRow r in dgSeq.Rows)
            {
                var cell = r.Cells["v"];
                if (cell.Value == null || !col.Items.Contains(cell.Value.ToString())) cell.Value = first;
            }
            _load = false;
        }

        void SetNbPlis(int nb)
        {
            int segs = Math.Max(1, nb + 1);
            while (piece.Segments.Count < segs) piece.Segments.Add(100);
            while (piece.Segments.Count > segs) piece.Segments.RemoveAt(piece.Segments.Count - 1);
            piece.Sequence.RemoveAll(o => o.Bend >= piece.NbPlis);
            ReloadGridsFromPiece();
            Recompute();
        }

        void ReloadGridsFromPiece()
        {
            _load = true;
            dgSeg.Rows.Clear();
            for (int i = 0; i < piece.Segments.Count; i++)
                dgSeg.Rows.Add((i + 1).ToString(), piece.Segments[i].ToString("0.#", CultureInfo.InvariantCulture));

            dgSeq.Rows.Clear();
            for (int i = 0; i < piece.Sequence.Count; i++)
            {
                var o = piece.Sequence[i];
                int r = dgSeq.Rows.Add((i + 1).ToString(), (o.Bend + 1).ToString(),
                    o.AngleCible.ToString("0.#", CultureInfo.InvariantCulture),
                    o.Sens == Sens.Haut ? "Haut" : "Bas",
                    ((int)o.V).ToString(), o.Reprise);
            }
            if (nNb != null) nNb.Value = Math.Min(nNb.Maximum, Math.Max(nNb.Minimum, (decimal)piece.NbPlis));
            _load = false;
            RebuildVColumn();
        }

        void ReadSeg()
        {
            for (int i = 0; i < dgSeg.Rows.Count && i < piece.Segments.Count; i++)
                piece.Segments[i] = ParseD(dgSeg.Rows[i].Cells["lg"].Value, piece.Segments[i]);
        }

        void ReadSeq()
        {
            var list = new List<Operation>();
            foreach (DataGridViewRow row in dgSeq.Rows)
            {
                if (row.IsNewRow) continue;
                int pli = (int)ParseD(row.Cells["pli"].Value, 1);
                var op = new Operation
                {
                    Bend = Math.Max(0, Math.Min(piece.NbPlis - 1, pli - 1)),
                    AngleCible = ParseD(row.Cells["ang"].Value, 90),
                    Sens = (row.Cells["sens"].Value as string) == "Bas" ? Sens.Bas : Sens.Haut,
                    V = ParseD(row.Cells["v"].Value, 16),
                    Reprise = row.Cells["rep"].Value is bool b && b
                };
                list.Add(op);
            }
            piece.Sequence = list;
        }

        void AddOp()
        {
            ReadSeq();
            int bend = piece.NbPlis > 0 ? 0 : 0;
            piece.Sequence.Add(new Operation { Bend = bend, AngleCible = 90, Sens = Sens.Haut, V = 16 });
            ReloadGridsFromPiece(); step = piece.Sequence.Count - 1; Recompute();
        }
        void DelOp()
        {
            ReadSeq();
            int idx = dgSeq.CurrentCell != null ? dgSeq.CurrentCell.RowIndex : piece.Sequence.Count - 1;
            if (idx >= 0 && idx < piece.Sequence.Count) piece.Sequence.RemoveAt(idx);
            ReloadGridsFromPiece(); step = Math.Min(step, piece.Sequence.Count - 1); Recompute();
        }
        void MoveOp(int dir)
        {
            ReadSeq();
            int idx = dgSeq.CurrentCell != null ? dgSeq.CurrentCell.RowIndex : -1;
            int j = idx + dir;
            if (idx < 0 || j < 0 || j >= piece.Sequence.Count) return;
            var tmp = piece.Sequence[idx]; piece.Sequence[idx] = piece.Sequence[j]; piece.Sequence[j] = tmp;
            ReloadGridsFromPiece();
            if (idx >= 0 && j < dgSeq.Rows.Count) dgSeq.CurrentCell = dgSeq.Rows[j].Cells["pli"];
            step = j; Recompute();
        }

        static double ParseD(object o, double def)
        {
            if (o == null) return def;
            string t = o.ToString().Trim().Replace(',', '.');
            return double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        // -------------------------------------------------- recompute --
        void SetStep(int s)
        {
            if (piece.Sequence.Count == 0) { step = 0; }
            else step = Math.Max(0, Math.Min(piece.Sequence.Count - 1, s));
            _load = true;
            tb.Maximum = Math.Max(0, piece.Sequence.Count - 1);
            tb.Value = Math.Min(tb.Maximum, Math.Max(tb.Minimum, step));
            _load = false;
            Redraw();
        }

        void Recompute()
        {
            _load = true;
            tb.Maximum = Math.Max(0, piece.Sequence.Count - 1);
            step = Math.Max(0, Math.Min(tb.Maximum, step));
            tb.Value = Math.Min(tb.Maximum, Math.Max(tb.Minimum, step));
            _load = false;
            BuildEnumeration();
            Redraw();
        }

        void Redraw()
        {
            StepState st = FoldEngine.Build(piece, step, cfg, curPoin, curMat, cfg.Embase);
            view.SetState(st, piece, StepColor(step));
            if (pupitre != null) pupitre.SetData(piece, step, curPoin, curMat, cfg.Embase);
            if (developpe != null) developpe.SetData(piece, step, Retournements());
            if (piece.Sequence.Count == 0)
            {
                lblStep.Text = "Aucune opération";
                lblAlert.Text = ""; return;
            }
            var op = piece.Sequence[step];
            double from = AngleBefore(step);
            lblStep.Text = $"Étape {step + 1}/{piece.Sequence.Count}  ·  Pli {op.Bend + 1}  ·  {from:0}°→{op.AngleCible:0}°  ·  {(op.Sens == Sens.Haut ? "Haut" : "Bas")}  ·  V{(int)op.V}";
            if (st.Collisions.Count == 0)
            {
                lblAlert.ForeColor = CVert; lblAlert.Text = "✔  Pas de collision à cette étape";
            }
            else
            {
                lblAlert.ForeColor = CRouge;
                var parts = new List<string>();
                foreach (var c in st.Collisions) parts.Add(c.Type + " — " + c.Detail);
                lblAlert.Text = "✖  " + string.Join("   |   ", parts);
            }
        }

        double AngleBefore(int s)
        {
            if (s < 0 || s >= piece.Sequence.Count) return 180;
            int bend = piece.Sequence[s].Bend;
            double a = 180;
            for (int i = 0; i < s; i++) if (piece.Sequence[i].Bend == bend) a = piece.Sequence[i].AngleCible;
            return a;
        }

        Color StepColor(int s)
        {
            if (s < 0 || s >= piece.Sequence.Count) return CBleu;
            return piece.Sequence[s].Reprise ? CVert : CBleu;
        }

        void BuildEnumeration()
        {
            rtSeq.Clear();
            var flips = Retournements();
            for (int i = 0; i < piece.Sequence.Count; i++)
            {
                var o = piece.Sequence[i];
                var st = FoldEngine.Build(piece, i, cfg, curPoin, curMat, cfg.Embase);
                double from = AngleBefore(i);
                bool hit = st.Collisions.Count > 0;
                bool flip = i < flips.Length && flips[i];
                Color col = hit ? CRouge : (o.Reprise ? CVert : CBleu);
                string etat = hit ? ("COLLISION: " + st.Collisions[0].Type) : (o.Reprise ? "reprise" : "direct");
                if (flip) etat += "  ⟲ retourner";
                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0,2}.  Pli {1,-2}  {2,3:0}°→{3,3:0}°  {4,-4}  V{5,-2}  · butée {6,4:0}  · {7}\n",
                    i + 1, o.Bend + 1, from, o.AngleCible, (o.Sens == Sens.Haut ? "Haut" : "Bas"),
                    (int)o.V, st.ButeeDistance, etat);
                rtSeq.SelectionStart = rtSeq.TextLength;
                rtSeq.SelectionColor = (i == step) ? CAccent : col;
                rtSeq.AppendText(line);
            }
        }
    }
}
