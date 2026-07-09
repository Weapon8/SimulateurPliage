using System;
using System.Collections.Generic;
using System.Drawing;
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
        static readonly Color CGrey   = Color.FromArgb(70, 78, 90);
        static readonly Color CSep    = Color.FromArgb(46, 53, 63);
        static readonly Color CBleu   = Color.FromArgb(63, 131, 235);   // pli direct
        static readonly Color CVert   = Color.FromArgb(63, 185, 80);    // pli avec reprise
        static readonly Color CRouge  = Color.FromArgb(229, 83, 75);    // collision
        static readonly Color CLockBg = Color.FromArgb(31, 36, 44);

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

        // ---- reglages machine : verrou + confirmation par ligne ----
        TableLayoutPanel left;
        Panel machContent;
        Button btnMachHead;
        CheckBox chkLock;
        Label lblDirty;
        readonly List<MField> mfields = new();
        bool machOpen = false;

        const int PANW   = 316;   // largeur utile du panneau gauche
        const int COLW   = 352;   // largeur de la colonne gauche
        const int MACH_H = 400;   // hauteur de l'encadre reglages deplie
        const int MACH_C = 40;    // hauteur replie

        sealed class MField
        {
            public Label Lab;
            public NumericUpDown Num;
            public Button Ok, Undo;
            public double Applied;
            public Action<double> Apply;
            public bool Dirty => Ok.Visible;
        }

        public MainForm()
        {
            Text = "Simulateur de pliage — collisions outillage · TolTem   [v0.9]";
            Width = 1360; Height = 900; StartPosition = FormStartPosition.CenterScreen;
            BackColor = CBack; ForeColor = CText; Font = new Font("Segoe UI", 9);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Font;
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
            }
            if (curMat != null) cfg.BlocLargeur = curMat.BlocLargeur;
        }

        // ------------------------------------------------ UI ----------
        void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = CGrey };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, COLW));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // colonne gauche : 4 rangees
            //   0 : entete (outillage / piece / pans)   -> absolue
            //   1 : titre sequence                       -> absolue
            //   2 : grille sequence + barre outils       -> etirable
            //   3 : encadre reglages machine             -> absolue, repliable
            left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = CPanel, Margin = new Padding(0) };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, MACH_C));
            root.Controls.Add(left, 0, 0);

            right = new Panel { Dock = DockStyle.Fill, BackColor = CBack, Margin = new Padding(0) };
            root.Controls.Add(right, 1, 0);

            // ---------------- rangee 0 : entete ----------------
            var head = new Panel { Dock = DockStyle.Fill, BackColor = CPanel, Margin = new Padding(0) };
            left.Controls.Add(head, 0, 0);
            int y = 6;

            y = Title(head, "OUTILLAGE", y, false);
            cbPoin = Combo(head, "Poinçon", PoinNames(), 0, ref y,
                i => { curPoin = lib.Poincons[i]; SyncCfgFromTools(); PushMachineFields(); view.SetTools(curMat, curPoin, cfg.Embase); Recompute(); });
            cbMat = Combo(head, "Matrice", MatNames(), Math.Max(0, lib.Matrices.IndexOf(curMat)), ref y,
                i => { curMat = lib.Matrices[i]; SyncCfgFromTools(); PushMachineFields(); view.SetTools(curMat, curPoin, cfg.Embase); RebuildVColumn(); Recompute(); });

            y = Title(head, "PIÈCE", y);
            nNb = Num(head, "Nombre de plis", 2, 0, 12, 1, 0, ref y, v => SetNbPlis((int)v));
            nEp = Num(head, "Épaisseur (mm)", 1.0, 0.4, 5, 0.1, 2, ref y, v => { piece.Epaisseur = v; Recompute(); });
            cbCotes = Combo(head, "Cotes", new[] { "intérieures", "extérieures" }, 0, ref y, i => { piece.CotesExterieures = i == 1; Recompute(); });

            y = Title(head, "PANS (longueurs mm)", y);
            dgSeg = Grid(head, 92, ref y);
            dgSeg.Columns.Add(TextCol("pan", "Pan", 56, true));
            dgSeg.Columns.Add(TextCol("lg", "Longueur", 150, false));
            dgSeg.Columns["lg"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgSeg.CellEndEdit += (s, e) => { if (!_load) { ReadSeg(); Recompute(); } };

            left.RowStyles[0] = new RowStyle(SizeType.Absolute, y + 6);

            // ---------------- rangee 1 : titre sequence ----------------
            var seqHead = new Panel { Dock = DockStyle.Fill, BackColor = CPanel, Margin = new Padding(0) };
            left.Controls.Add(seqHead, 0, 1);
            seqHead.Controls.Add(new Panel { Left = 10, Top = 4, Width = PANW, Height = 1, BackColor = CSep });
            seqHead.Controls.Add(new Label { Text = "SÉQUENCE DE PLIAGE", Left = 12, Top = 12, Width = PANW,
                ForeColor = CAccent, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) });

            // ---------------- rangee 2 : grille sequence (etirable) ----------------
            var seqBox = new Panel { Dock = DockStyle.Fill, BackColor = CPanel, Padding = new Padding(12, 0, 12, 6), Margin = new Padding(0) };
            left.Controls.Add(seqBox, 0, 2);

            var barSeq = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, BackColor = CPanel };
            barSeq.Controls.Add(Btn("+ étape", 74, AddOp));
            barSeq.Controls.Add(Btn("–", 34, DelOp));
            barSeq.Controls.Add(Btn("↑", 34, () => MoveOp(-1)));
            barSeq.Controls.Add(Btn("↓", 34, () => MoveOp(+1)));
            barSeq.Controls.Add(Btn("Exemple U", 92, () => { piece = Piece.Demo(); ReloadGridsFromPiece(); step = 0; Recompute(); }));
            seqBox.Controls.Add(barSeq);

            dgSeq = new DataGridView
            {
                Dock = DockStyle.Fill, BackgroundColor = CInput, BorderStyle = BorderStyle.None, GridColor = CGrey,
                RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false, EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Segoe UI", 9.5f)
            };
            dgSeq.RowTemplate.Height = 30;
            dgSeq.DefaultCellStyle.BackColor = CInput; dgSeq.DefaultCellStyle.ForeColor = CText;
            dgSeq.DefaultCellStyle.SelectionBackColor = CBtn; dgSeq.DefaultCellStyle.SelectionForeColor = CText;
            dgSeq.DefaultCellStyle.Padding = new Padding(2, 4, 2, 4);
            dgSeq.ColumnHeadersDefaultCellStyle.BackColor = CPanel;
            dgSeq.ColumnHeadersDefaultCellStyle.ForeColor = CMuted;
            dgSeq.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            dgSeq.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgSeq.ColumnHeadersHeight = 28;

            AddFillCol(dgSeq, TextCol("ord", "N°", 30, true), 12);
            AddFillCol(dgSeq, TextCol("pli", "Pli", 38, false), 14);
            AddFillCol(dgSeq, TextCol("ang", "Angle°", 54, false), 20);
            AddFillCol(dgSeq, ComboCol("sens", "Sens", new[] { "Haut", "Bas" }, 58), 22);
            AddFillCol(dgSeq, ComboCol("v", "V", VStrings(), 46), 16);
            AddFillCol(dgSeq, new DataGridViewCheckBoxColumn { Name = "rep", HeaderText = "Reprise" }, 20);

            dgSeq.CellEndEdit += (s, e) => { if (!_load) { ReadSeq(); Recompute(); } };
            dgSeq.CurrentCellDirtyStateChanged += (s, e) => { if (dgSeq.IsCurrentCellDirty) dgSeq.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            dgSeq.DataError += (s, e) => { e.ThrowException = false; };
            dgSeq.SelectionChanged += (s, e) =>
            {
                if (_load || dgSeq.CurrentCell == null) return;
                int r = dgSeq.CurrentCell.RowIndex;
                if (r >= 0 && r < piece.Sequence.Count && r != step) SetStep(r);
            };
            seqBox.Controls.Add(dgSeq);
            dgSeq.BringToFront();

            // ---------------- rangee 3 : reglages machine (repliable + verrou) ----------------
            var mach = new Panel { Dock = DockStyle.Fill, BackColor = CLockBg, Margin = new Padding(0) };
            left.Controls.Add(mach, 0, 3);

            var machHead = new Panel { Dock = DockStyle.Top, Height = MACH_C, BackColor = CLockBg };
            mach.Controls.Add(machHead);
            machHead.Controls.Add(new Panel { Left = 0, Top = 0, Width = COLW, Height = 1, BackColor = CSep });

            btnMachHead = new Button
            {
                Text = "▸  RÉGLAGES MACHINE", Left = 8, Top = 7, Width = 190, Height = 26,
                FlatStyle = FlatStyle.Flat, BackColor = CLockBg, ForeColor = CAccent,
                TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnMachHead.FlatAppearance.BorderSize = 0;
            btnMachHead.Click += (s, e) => ToggleMach();
            machHead.Controls.Add(btnMachHead);

            lblDirty = new Label { Left = 200, Top = 12, Width = 60, ForeColor = CAccent, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            machHead.Controls.Add(lblDirty);

            chkLock = new CheckBox { Text = "🔒", Left = 296, Top = 9, Width = 44, Height = 24, Checked = true,
                Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, BackColor = CBtn, ForeColor = CText,
                TextAlign = ContentAlignment.MiddleCenter };
            chkLock.FlatAppearance.BorderColor = CGrey;
            chkLock.CheckedChanged += (s, e) => ApplyLock();
            var tip = new ToolTip(); tip.SetToolTip(chkLock, "Verrouiller / déverrouiller les réglages machine");
            machHead.Controls.Add(chkLock);

            machContent = new Panel { Dock = DockStyle.Fill, BackColor = CLockBg, AutoScroll = true, Visible = false, Padding = new Padding(0, 2, 0, 8) };
            mach.Controls.Add(machContent);
            machContent.BringToFront();

            int my = 2;
            my = SubTitle(machContent, "Poinçon", my);
            MNum(machContent, "Hauteur", cfg.PoinconHauteur, ref my, v => { cfg.PoinconHauteur = v; if (curPoin != null) curPoin.Hauteur = v; view.SetTools(curMat, curPoin, cfg.Embase); });
            MNum(machContent, "Angle pointe (°)", cfg.PoinconAngleDeg, ref my, v => cfg.PoinconAngleDeg = v);
            MNum(machContent, "Largeur pointe", cfg.PoinconPointeLg, ref my, v => cfg.PoinconPointeLg = v);

            my = SubTitle(machContent, "Col de cygne  (secours — profil vectoriel prioritaire)", my);
            MNum(machContent, "Retrait", cfg.ColRetrait, ref my, v => cfg.ColRetrait = v);
            MNum(machContent, "Hauteur", cfg.ColHauteur, ref my, v => cfg.ColHauteur = v);

            my = SubTitle(machContent, "Butée & tablier", my);
            MNum(machContent, "Tablier déport", cfg.TablierDeport, ref my, v => cfg.TablierDeport = v);
            MNum(machContent, "Hauteur libre (repère)", cfg.HauteurLibre, ref my, v => cfg.HauteurLibre = v);
            MNum(machContent, "Butée arrière max", cfg.ButeeMax, ref my, v => cfg.ButeeMax = v);

            my = SubTitle(machContent, "Embases", my);
            MNum(machContent, "Porte-poinçon hauteur", cfg.Embase.PortePoinconH, ref my, v => cfg.Embase.PortePoinconH = v);
            MNum(machContent, "Porte-poinçon largeur", cfg.Embase.PortePoinconLg, ref my, v => cfg.Embase.PortePoinconLg = v);
            MNum(machContent, "Semelle hauteur", cfg.Embase.SemelleH, ref my, v => cfg.Embase.SemelleH = v);
            MNum(machContent, "Semelle largeur", cfg.Embase.SemelleLg, ref my, v => cfg.Embase.SemelleLg = v);

            var barMach = new FlowLayoutPanel { Left = 8, Top = my + 6, Width = PANW, Height = 34, BackColor = CLockBg };
            barMach.Controls.Add(Btn("Tout appliquer", 106, ApplyAllFields));
            barMach.Controls.Add(Btn("Tout annuler", 100, RevertAllFields));
            machContent.Controls.Add(barMach);

            ApplyLock();

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
            var bPrev = Btn("◀", 44, () => SetStep(step - 1)); bPrev.Left = 10; bPrev.Top = 9; bPrev.Height = 34; ctrl.Controls.Add(bPrev);
            var bNext = Btn("▶", 44, () => SetStep(step + 1)); bNext.Left = 58; bNext.Top = 9; bNext.Height = 34; ctrl.Controls.Add(bNext);
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

        // ---------------- reglages machine : verrou / confirmation ----------------
        void ToggleMach()
        {
            machOpen = !machOpen;
            left.RowStyles[3] = new RowStyle(SizeType.Absolute, machOpen ? MACH_H : MACH_C);
            machContent.Visible = machOpen;
            btnMachHead.Text = (machOpen ? "▾  " : "▸  ") + "RÉGLAGES MACHINE";
        }

        void ApplyLock()
        {
            bool locked = chkLock.Checked;
            chkLock.Text = locked ? "🔒" : "🔓";
            chkLock.BackColor = locked ? CBtn : Color.FromArgb(70, 45, 14);
            foreach (var f in mfields)
            {
                f.Num.Enabled = !locked;
                f.Num.BackColor = locked ? CLockBg : CInput;
                f.Lab.ForeColor = locked ? Color.FromArgb(100, 108, 120) : CMuted;
                if (locked && f.Dirty) Revert(f);
            }
            UpdateDirty();
        }

        void MarkDirty(MField f)
        {
            bool dirty = Math.Abs((double)f.Num.Value - f.Applied) > 1e-9;
            f.Ok.Visible = f.Undo.Visible = dirty;
            f.Num.ForeColor = dirty ? CAccent : CText;
            UpdateDirty();
        }

        void UpdateDirty()
        {
            int n = 0; foreach (var f in mfields) if (f.Dirty) n++;
            lblDirty.Text = n > 0 ? $"{n} à valider" : "";
        }

        void Confirm(MField f)
        {
            f.Applied = (double)f.Num.Value;
            f.Apply(f.Applied);
            f.Ok.Visible = f.Undo.Visible = false;
            f.Num.ForeColor = CText;
            UpdateDirty();
            Recompute();
        }

        void Revert(MField f)
        {
            _load = true; f.Num.Value = Clamp(f.Num, f.Applied); _load = false;
            f.Ok.Visible = f.Undo.Visible = false;
            f.Num.ForeColor = CText;
            UpdateDirty();
        }

        void ApplyAllFields()
        {
            bool any = false;
            foreach (var f in mfields) if (f.Dirty) { f.Applied = (double)f.Num.Value; f.Apply(f.Applied); f.Ok.Visible = f.Undo.Visible = false; f.Num.ForeColor = CText; any = true; }
            UpdateDirty();
            if (any) Recompute();
        }

        void RevertAllFields()
        {
            foreach (var f in mfields) if (f.Dirty) Revert(f);
        }

        // recharge les champs depuis cfg (changement d'outil) sans passer par la confirmation
        void PushMachineFields()
        {
            if (mfields.Count == 0) return;
            _load = true;
            mfields[0].Applied = cfg.PoinconHauteur;  mfields[0].Num.Value = Clamp(mfields[0].Num, cfg.PoinconHauteur);
            mfields[1].Applied = cfg.PoinconAngleDeg; mfields[1].Num.Value = Clamp(mfields[1].Num, cfg.PoinconAngleDeg);
            _load = false;
            foreach (var f in mfields) { f.Ok.Visible = f.Undo.Visible = false; f.Num.ForeColor = CText; }
            UpdateDirty();
        }

        static decimal Clamp(NumericUpDown n, double v)
            => Math.Min(n.Maximum, Math.Max(n.Minimum, (decimal)v));

        void ShowView(int mode)
        {
            view.Visible = mode == 0;
            pupitre.Visible = mode == 1;
            developpe.Visible = mode == 2;
            if (mode == 0) view.BringToFront();
            else if (mode == 1) pupitre.BringToFront();
            else developpe.BringToFront();
        }

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
                p.Controls.Add(new Panel { Left = 10, Top = y + 6, Width = PANW, Height = 1, BackColor = CSep });
                y += 12;
            }
            p.Controls.Add(new Label { Text = t, Left = 12, Top = y + 6, Width = PANW, ForeColor = CAccent, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) });
            return y + 30;
        }

        int SubTitle(Panel p, string t, int y)
        {
            p.Controls.Add(new Label { Text = t, Left = 10, Top = y + 6, Width = PANW, ForeColor = CMuted, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });
            return y + 24;
        }

        NumericUpDown Num(Panel p, string lab, double v, double min, double max, double inc, int dec, ref int y, Action<double> onCh)
        {
            var l = new Label { Text = lab, Left = 12, Top = y + 4, Width = 150, ForeColor = CText };
            var n = new NumericUpDown { Left = 166, Top = y, Width = 140, Minimum = (decimal)min, Maximum = (decimal)max,
                Increment = (decimal)inc, DecimalPlaces = dec, Value = (decimal)v, BackColor = CInput, ForeColor = CText, BorderStyle = BorderStyle.FixedSingle };
            n.ValueChanged += (s, e) => { if (!_load) onCh((double)n.Value); };
            p.Controls.Add(l); p.Controls.Add(n); y += 30; return n;
        }

        // champ machine : modification -> orange, puis ✓ (appliquer) ou ↺ (annuler). Rien n'est applique avant.
        void MNum(Panel p, string lab, double v, ref int y, Action<double> apply)
        {
            var l = new Label { Text = lab, Left = 16, Top = y + 4, Width = 152, ForeColor = CMuted, Font = new Font("Segoe UI", 8.5f) };
            var n = new NumericUpDown { Left = 172, Top = y, Width = 84, Minimum = 0, Maximum = 5000, DecimalPlaces = 1,
                Increment = 1, Value = (decimal)v, BackColor = CInput, ForeColor = CText, BorderStyle = BorderStyle.FixedSingle, Enabled = false };
            var ok = new Button { Text = "✓", Left = 260, Top = y - 1, Width = 26, Height = 24, Visible = false,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(28, 70, 36), ForeColor = CVert, Margin = new Padding(0) };
            var un = new Button { Text = "↺", Left = 288, Top = y - 1, Width = 26, Height = 24, Visible = false,
                FlatStyle = FlatStyle.Flat, BackColor = CBtn, ForeColor = CMuted, Margin = new Padding(0) };
            ok.FlatAppearance.BorderColor = CGrey; un.FlatAppearance.BorderColor = CGrey;

            var f = new MField { Lab = l, Num = n, Ok = ok, Undo = un, Applied = v, Apply = apply };
            n.ValueChanged += (s, e) => { if (!_load) MarkDirty(f); };
            n.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && f.Dirty) { Confirm(f); e.SuppressKeyPress = true; }
                                     else if (e.KeyCode == Keys.Escape && f.Dirty) { Revert(f); e.SuppressKeyPress = true; } };
            ok.Click += (s, e) => Confirm(f);
            un.Click += (s, e) => Revert(f);

            var tip = new ToolTip();
            tip.SetToolTip(ok, "Appliquer cette valeur (Entrée)");
            tip.SetToolTip(un, "Annuler la modification (Échap)");

            mfields.Add(f);
            p.Controls.Add(l); p.Controls.Add(n); p.Controls.Add(ok); p.Controls.Add(un);
            y += 28;
        }

        ComboBox Combo(Panel p, string lab, string[] items, int sel, ref int y, Action<int> onCh)
        {
            var l = new Label { Text = lab, Left = 12, Top = y + 4, Width = 150, ForeColor = CText };
            var c = new ComboBox { Left = 166, Top = y, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = CInput, ForeColor = CText, FlatStyle = FlatStyle.Flat };
            c.Items.AddRange(items); c.SelectedIndex = sel;
            c.SelectedIndexChanged += (s, e) => { if (!_load) onCh(c.SelectedIndex); };
            p.Controls.Add(l); p.Controls.Add(c); y += 30; return c;
        }

        DataGridView Grid(Panel p, int h, ref int y)
        {
            var g = new DataGridView { Left = 12, Top = y, Width = PANW - 4, Height = h, BackgroundColor = CInput,
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

        static void AddFillCol(DataGridView g, DataGridViewColumn c, int weight)
        {
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            c.FillWeight = weight;
            g.Columns.Add(c);
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
                dgSeq.Rows.Add((i + 1).ToString(), (o.Bend + 1).ToString(),
                    o.AngleCible.ToString("0.#", CultureInfo.InvariantCulture),
                    o.Sens == Sens.Haut ? "Haut" : "Bas",
                    ((int)o.V).ToString(), o.Reprise);
            }
            if (nNb != null) nNb.Value = Clamp(nNb, piece.NbPlis);
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
                list.Add(new Operation
                {
                    Bend = Math.Max(0, Math.Min(piece.NbPlis - 1, pli - 1)),
                    AngleCible = ParseD(row.Cells["ang"].Value, 90),
                    Sens = (row.Cells["sens"].Value as string) == "Bas" ? Sens.Bas : Sens.Haut,
                    V = ParseD(row.Cells["v"].Value, 16),
                    Reprise = row.Cells["rep"].Value is bool b && b
                });
            }
            piece.Sequence = list;
        }

        void AddOp()
        {
            ReadSeq();
            piece.Sequence.Add(new Operation { Bend = 0, AngleCible = 90, Sens = Sens.Haut, V = 16 });
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
            (piece.Sequence[idx], piece.Sequence[j]) = (piece.Sequence[j], piece.Sequence[idx]);
            ReloadGridsFromPiece();
            if (j < dgSeq.Rows.Count) dgSeq.CurrentCell = dgSeq.Rows[j].Cells["pli"];
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
            step = piece.Sequence.Count == 0 ? 0 : Math.Max(0, Math.Min(piece.Sequence.Count - 1, s));
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
            _load = true;
            for (int i = 0; i < piece.Sequence.Count; i++)
            {
                var o = piece.Sequence[i];
                var st = FoldEngine.Build(piece, i, cfg, curPoin, curMat, cfg.Embase);
                double from = AngleBefore(i);
                bool hit = st.Collisions.Count > 0;
                bool flip = i < flips.Length && flips[i];
                Color col = hit ? CRouge : (o.Reprise ? CVert : CBleu);

                // teinte la ligne correspondante dans la grille — lisible d'un coup d'oeil
                if (i < dgSeq.Rows.Count)
                {
                    dgSeq.Rows[i].DefaultCellStyle.ForeColor = col;
                    dgSeq.Rows[i].DefaultCellStyle.SelectionForeColor = col;
                    dgSeq.Rows[i].DefaultCellStyle.BackColor = (i == step) ? Color.FromArgb(40, 30, 12) : CInput;
                }

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
            _load = false;
        }
    }
}
