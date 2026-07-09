using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace SimulateurPliage
{
    // Vue "pupitre" facon commande numerique Cybelec/Delem, EDITABLE.
    // Une ligne = une operation de pliage. La colonne R (butee) EST le pan cote butee :
    // pans et plis sont la meme donnee, il n'y a plus de table de pans separee.
    // La derniere ligne "fin" porte le dernier pan (celui qui n'a pas de pli apres lui).
    //
    // IMPORTANT : tous les evenements sortants sont emis en differe (BeginInvoke).
    // Recharger la grille depuis l'interieur d'un CellEndEdit / SelectionChanged
    // provoque "appel reentrant a SetCurrentCellAddressCore".
    public class PupitrePanel : Panel
    {
        public event Action Edited;              // une cellule a ete validee
        public event Action<int> StepPicked;     // l'operateur a clique une ligne
        public event Action AddBendRequested;    // + pli   (ajoute une ligne de pli a la piece)
        public event Action AddOpRequested;      // + étape (2e passe sur un pli -> reprise)
        public event Action DelOpRequested;
        public event Action<int> MoveOpRequested;

        readonly MachineConfig cfg;
        Piece piece;
        int cur = -1;
        Poincon poin;
        Matrice mat;
        Embase emb;
        bool _load;      // remplissage en cours : on ignore les evenements de la grille
        bool _reload;    // garde anti-reentrance sur Reload()

        DataGridView dg;
        Label lblTitle, lblFoot;

        static readonly Color CBg    = Color.FromArgb(12, 15, 20);
        static readonly Color CRow   = Color.FromArgb(22, 27, 34);
        static readonly Color CCurBg = Color.FromArgb(40, 30, 12);
        static readonly Color CHead  = Color.FromArgb(140, 150, 162);
        static readonly Color CGreen = Color.FromArgb(80, 230, 120);
        static readonly Color COrange= Color.FromArgb(255, 170, 60);
        static readonly Color CRed   = Color.FromArgb(235, 90, 80);
        static readonly Color CGrey  = Color.FromArgb(50, 58, 68);
        static readonly Color CBtn   = Color.FromArgb(43, 49, 59);
        static readonly Color CTxt   = Color.FromArgb(230, 235, 240);
        static readonly Color CFin   = Color.FromArgb(120, 130, 145);

        // index de ligne courant (pour les actions supprimer / monter / descendre)
        public int CurrentRow => dg?.CurrentCell?.RowIndex ?? -1;

        public PupitrePanel(MachineConfig c)
        {
            cfg = c;
            DoubleBuffered = true;
            BackColor = CBg;
            Padding = new Padding(14, 12, 14, 10);
            BuildGrid();
        }

        void BuildGrid()
        {
            lblTitle = new Label
            {
                Dock = DockStyle.Top, Height = 30, Text = "PUPITRE — séquence de pliage",
                ForeColor = COrange, BackColor = CBg, Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };

            lblFoot = new Label
            {
                Dock = DockStyle.Bottom, Height = 26, ForeColor = CHead, BackColor = CBg,
                Font = new Font("Consolas", 9.5f), Padding = new Padding(2, 6, 0, 0)
            };

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, BackColor = CBg, Padding = new Padding(0, 6, 0, 0) };
            bar.Controls.Add(Btn("+ pli", 84, () => Defer(() => AddBendRequested?.Invoke()), COrange));
            bar.Controls.Add(Btn("+ étape", 84, () => Defer(() => AddOpRequested?.Invoke())));
            bar.Controls.Add(Btn("–", 44, () => Defer(() => DelOpRequested?.Invoke())));
            bar.Controls.Add(Btn("↑", 44, () => Defer(() => MoveOpRequested?.Invoke(-1))));
            bar.Controls.Add(Btn("↓", 44, () => Defer(() => MoveOpRequested?.Invoke(+1))));

            dg = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = CBg, BorderStyle = BorderStyle.None, GridColor = CGrey,
                RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false, AllowUserToResizeColumns = false,
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Consolas", 14f, FontStyle.Bold),
                ColumnHeadersHeight = 34,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            dg.RowTemplate.Height = 46;
            dg.DefaultCellStyle.BackColor = CRow;
            dg.DefaultCellStyle.ForeColor = CGreen;
            dg.DefaultCellStyle.SelectionBackColor = Color.FromArgb(34, 42, 52);
            dg.DefaultCellStyle.SelectionForeColor = CGreen;
            dg.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            dg.ColumnHeadersDefaultCellStyle.BackColor = CBg;
            dg.ColumnHeadersDefaultCellStyle.ForeColor = CHead;
            dg.ColumnHeadersDefaultCellStyle.Font = new Font("Consolas", 10f, FontStyle.Bold);

            Fill(dg, new DataGridViewTextBoxColumn { Name = "ord", HeaderText = "N°", ReadOnly = true }, 8);
            Fill(dg, new DataGridViewTextBoxColumn { Name = "pli", HeaderText = "PLI" }, 10);
            Fill(dg, new DataGridViewTextBoxColumn { Name = "r",   HeaderText = "R butée (int.)" }, 24);
            Fill(dg, new DataGridViewTextBoxColumn { Name = "ang", HeaderText = "ANGLE" }, 16);
            Fill(dg, ComboCol("sens", "SENS", new[] { "Haut", "Bas" }), 16);
            Fill(dg, ComboCol("v", "V", new[] { "16" }), 12);
            Fill(dg, new DataGridViewCheckBoxColumn { Name = "rep", HeaderText = "REPRISE" }, 14);

            // --- evenements : on relit tout de suite, on previent EN DIFFERE ---
            dg.CellEndEdit += (s, e) =>
            {
                if (_load) return;
                ReadBack();
                Defer(() => Edited?.Invoke());
            };
            dg.CurrentCellDirtyStateChanged += (s, e) => { if (dg.IsCurrentCellDirty) dg.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            dg.DataError += (s, e) => { e.ThrowException = false; };
            dg.SelectionChanged += (s, e) =>
            {
                if (_load || _reload || piece == null || dg.CurrentCell == null) return;
                int r = dg.CurrentCell.RowIndex;
                if (r >= 0 && r < piece.Sequence.Count && r != cur) Defer(() => StepPicked?.Invoke(r));
            };

            Controls.Add(dg);
            Controls.Add(bar);
            Controls.Add(lblFoot);
            Controls.Add(lblTitle);
            dg.BringToFront();
        }

        // sort de la pile d'evenements de la grille avant de la recharger
        void Defer(Action a)
        {
            if (IsHandleCreated) BeginInvoke(a);
            else a();
        }

        static void Fill(DataGridView g, DataGridViewColumn c, int weight)
        {
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            c.FillWeight = weight;
            g.Columns.Add(c);
        }

        static DataGridViewComboBoxColumn ComboCol(string name, string head, string[] items)
        {
            var c = new DataGridViewComboBoxColumn { Name = name, HeaderText = head, FlatStyle = FlatStyle.Flat };
            c.Items.AddRange(items); return c;
        }

        Button Btn(string t, int w, Action onClick, Color? fg = null)
        {
            var b = new Button { Text = t, Width = w, Height = 30, FlatStyle = FlatStyle.Flat,
                BackColor = CBtn, ForeColor = fg ?? CTxt, Margin = new Padding(3, 0, 3, 0),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            b.FlatAppearance.BorderColor = CGrey;
            b.Click += (s, e) => onClick();
            return b;
        }

        public void SetData(Piece p, int step, Poincon pn, Matrice mt, Embase eb)
        {
            piece = p; cur = step; poin = pn; mat = mt; emb = eb;
            Reload();
        }

        string[] VStrings()
        {
            var l = new List<string>();
            if (mat != null) foreach (var vf in mat.Vs) l.Add(((int)vf.V).ToString());
            if (l.Count == 0) l.Add("16");
            return l.ToArray();
        }

        void Reload()
        {
            if (piece == null || _reload) return;
            _reload = true; _load = true;
            try
            {
                if (dg.IsCurrentCellInEditMode) dg.EndEdit();

                int cr = dg.CurrentCell?.RowIndex ?? -1, cc = dg.CurrentCell?.ColumnIndex ?? -1;

                var vcol = (DataGridViewComboBoxColumn)dg.Columns["v"];
                vcol.Items.Clear();
                foreach (var s in VStrings()) vcol.Items.Add(s);
                string v0 = VStrings()[0];

                dg.CurrentCell = null;      // evite tout SetCurrentCellAddressCore pendant Clear()
                dg.Rows.Clear();

                int n = piece.Sequence.Count;
                for (int i = 0; i < n; i++)
                {
                    var o = piece.Sequence[i];
                    string vv = ((int)o.V).ToString();
                    if (!vcol.Items.Contains(vv)) vv = v0;
                    dg.Rows.Add(
                        (i + 1).ToString("00"),
                        "P" + (o.Bend + 1),
                        piece.ButeeInt(o.Bend).ToString("0.0", CultureInfo.InvariantCulture),
                        o.AngleCible.ToString("0", CultureInfo.InvariantCulture) + "\u00b0",
                        o.Sens == Sens.Haut ? "Haut" : "Bas",
                        vv,
                        o.Reprise);
                }

                // ligne FIN : le dernier pan, sans pli apres lui
                int fr = dg.Rows.Add("—", "fin", piece.ButeeInt(piece.NbPlis).ToString("0.0", CultureInfo.InvariantCulture), "", null, null, false);
                var frow = dg.Rows[fr];
                foreach (DataGridViewCell c in frow.Cells) c.ReadOnly = true;
                frow.Cells["r"].ReadOnly = false;
                frow.DefaultCellStyle.ForeColor = CFin;
                frow.DefaultCellStyle.SelectionForeColor = CFin;
                frow.DefaultCellStyle.BackColor = Color.FromArgb(18, 22, 28);

                // couleurs par ligne : rouge collision, orange etape courante
                for (int i = 0; i < n; i++)
                {
                    var st = FoldEngine.Build(piece, i, cfg, poin, mat, emb);
                    bool hit = st.Collisions.Count > 0;
                    var row = dg.Rows[i];
                    Color fg = hit ? CRed : (i == cur ? COrange : CGreen);
                    row.DefaultCellStyle.ForeColor = fg;
                    row.DefaultCellStyle.SelectionForeColor = fg;
                    row.DefaultCellStyle.BackColor = (i == cur) ? CCurBg : CRow;
                    row.DefaultCellStyle.SelectionBackColor = (i == cur) ? CCurBg : Color.FromArgb(34, 42, 52);

                    double from = AngleBefore(i);
                    string tip = $"{from:0}°→{piece.Sequence[i].AngleCible:0}°  " +
                                 (piece.Sequence[i].Reprise ? "reprise" : "direct") +
                                 (hit ? "   ! " + st.Collisions[0].Type : "");
                    foreach (DataGridViewCell c in row.Cells) c.ToolTipText = tip;
                }

                if (cr >= 0 && cr < dg.Rows.Count && cc >= 0 && cc < dg.Columns.Count)
                    dg.CurrentCell = dg.Rows[cr].Cells[cc];
                else if (cur >= 0 && cur < dg.Rows.Count)
                    dg.CurrentCell = dg.Rows[cur].Cells["r"];

                string ep = piece.Epaisseur.ToString("0.##", CultureInfo.InvariantCulture);
                string mode = piece.CotesExterieures ? "extérieures (R converti en int.)" : "intérieures";
                lblFoot.Text = $"ép {ep} mm  ·  saisie {mode}  ·  R = cote intérieure lue à la butée arrière  ·  ligne « fin » = dernier pan";
            }
            finally { _load = false; _reload = false; }
        }

        // relit toute la grille dans la Piece (source unique de verite)
        void ReadBack()
        {
            if (piece == null) return;
            int n = piece.Sequence.Count;
            var list = new List<Operation>(n);

            for (int i = 0; i < dg.Rows.Count; i++)
            {
                var row = dg.Rows[i];

                if (i == n)   // ligne FIN
                {
                    piece.SetButeeInt(piece.NbPlis, ParseD(row.Cells["r"].Value, piece.ButeeInt(piece.NbPlis)));
                    continue;
                }

                int pli = (int)ParseD(row.Cells["pli"].Value, i + 1);
                int bend = Math.Max(0, Math.Min(Math.Max(0, piece.NbPlis - 1), pli - 1));

                piece.SetButeeInt(bend, ParseD(row.Cells["r"].Value, piece.ButeeInt(bend)));

                list.Add(new Operation
                {
                    Bend = bend,
                    AngleCible = Math.Max(1, Math.Min(179, ParseD(row.Cells["ang"].Value, 90))),
                    Sens = (row.Cells["sens"].Value as string) == "Bas" ? Sens.Bas : Sens.Haut,
                    V = ParseD(row.Cells["v"].Value, 16),
                    Reprise = row.Cells["rep"].Value is bool b && b
                });
            }
            piece.Sequence = list;
        }

        double AngleBefore(int s)
        {
            if (piece == null || s < 0 || s >= piece.Sequence.Count) return 180;
            int bend = piece.Sequence[s].Bend;
            double a = 180;
            for (int i = 0; i < s; i++) if (piece.Sequence[i].Bend == bend) a = piece.Sequence[i].AngleCible;
            return a;
        }

        // "P2" -> 2 ; "90°" -> 90 ; "40,5" -> 40.5
        static double ParseD(object o, double def)
        {
            if (o == null) return def;
            string t = o.ToString().Trim().Replace(',', '.').Replace("\u00b0", "").Replace("P", "").Replace("p", "");
            return double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;
        }
    }
}
