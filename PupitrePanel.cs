using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace SimulateurPliage
{
    // Vue "pupitre" facon commande numerique Cybelec/Delem, EDITABLE.
    // Une ligne = une operation de pliage. La colonne R (butee) EST le pan cote butee.
    // La derniere ligne "fin" porte le dernier pan (celui qui n'a pas de pli apres lui).
    //
    // REGLE D'OR : on ne mute JAMAIS les styles de lignes ou de cellules a la main.
    // Chaque affectation de row.DefaultCellStyle / cell.ToolTipText invalide la ligne et
    // detruit le controle d'edition que WinForms vient d'installer -> la cellule devient
    // impossible a saisir. Les couleurs sont calculees a la volee dans CellFormatting,
    // qui se declenche au dessin et n'interfere avec rien.
    public class PupitrePanel : Panel
    {
        public event Action Edited;                 // une valeur a change
        public event Action<int> StepPicked;        // l'operateur a clique une ligne
        public event Action AddBendRequested;       // + pli
        public event Action AddOpRequested;         // + étape (2e passe -> reprise)
        public event Action DelOpRequested;         // supprime la passe courante
        public event Action SortRequested;          // remet le programme dans l'ordre des plis
        public event Action<int> DeleteRowRequested;// ✕ : supprime la passe (et le pli si derniere)
        public event Action<int> MoveOpRequested;

        readonly MachineConfig cfg;
        Piece piece;
        int cur = -1;
        Poincon poin;
        Matrice mat;
        Embase emb;
        bool _load;
        bool[] hits = new bool[0];   // collision par operation, recalcule a chaque etape

        DataGridView dg;
        Label lblTitle, lblFoot;

        static readonly Color CBg    = Color.FromArgb(12, 15, 20);
        static readonly Color CRow   = Color.FromArgb(22, 27, 34);
        static readonly Color CCurBg = Color.FromArgb(40, 30, 12);
        static readonly Color CSelBg = Color.FromArgb(34, 42, 52);
        static readonly Color CFinBg = Color.FromArgb(18, 22, 28);
        static readonly Color CHead  = Color.FromArgb(140, 150, 162);
        static readonly Color CGreen = Color.FromArgb(80, 230, 120);
        static readonly Color COrange= Color.FromArgb(255, 170, 60);
        static readonly Color CRed   = Color.FromArgb(235, 90, 80);
        static readonly Color CGrey  = Color.FromArgb(50, 58, 68);
        static readonly Color CBtn   = Color.FromArgb(43, 49, 59);
        static readonly Color CTxt   = Color.FromArgb(230, 235, 240);
        static readonly Color CFin   = Color.FromArgb(120, 130, 145);

        public int CurrentRow => dg?.CurrentCell?.RowIndex ?? -1;

        int FinRow => piece != null ? piece.Sequence.Count : -1;

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

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, BackColor = CBg, Padding = new Padding(0, 8, 0, 0) };
            bar.Controls.Add(Btn("+ pli", 110, () => AddBendRequested?.Invoke(), COrange));
            bar.Controls.Add(Btn("+ étape", 110, () => AddOpRequested?.Invoke()));
            bar.Controls.Add(Btn("–", 58, () => DelOpRequested?.Invoke()));
            bar.Controls.Add(Btn("↑", 58, () => MoveOpRequested?.Invoke(-1)));
            bar.Controls.Add(Btn("↓", 58, () => MoveOpRequested?.Invoke(+1)));
            bar.Controls.Add(Btn("Trier", 92, () => SortRequested?.Invoke()));

            dg = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = CBg, BorderStyle = BorderStyle.None, GridColor = CGrey,
                RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false, AllowUserToResizeColumns = false,
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode = DataGridViewEditMode.EditOnEnter,     // un clic = saisie
                Font = new Font("Consolas", 14f, FontStyle.Bold),
                ColumnHeadersHeight = 34,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            dg.RowTemplate.Height = 46;
            dg.DefaultCellStyle.BackColor = CRow;
            dg.DefaultCellStyle.ForeColor = CGreen;
            dg.DefaultCellStyle.SelectionBackColor = CSelBg;
            dg.DefaultCellStyle.SelectionForeColor = CGreen;
            dg.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            dg.ColumnHeadersDefaultCellStyle.BackColor = CBg;
            dg.ColumnHeadersDefaultCellStyle.ForeColor = CHead;
            dg.ColumnHeadersDefaultCellStyle.Font = new Font("Consolas", 10f, FontStyle.Bold);

            Fill(dg, new DataGridViewTextBoxColumn { Name = "ord", HeaderText = "N°" }, 8);
            Fill(dg, new DataGridViewTextBoxColumn { Name = "pli", HeaderText = "PLI" }, 10);
            Fill(dg, new DataGridViewTextBoxColumn { Name = "r",   HeaderText = "R butée (int.)" }, 24);
            Fill(dg, new DataGridViewTextBoxColumn { Name = "ang", HeaderText = "ANGLE °" }, 16);
            Fill(dg, ComboCol("sens", "SENS", new[] { "Haut", "Bas" }), 16);
            Fill(dg, ComboCol("v", "V", new[] { "16" }), 12);
            Fill(dg, new DataGridViewCheckBoxColumn { Name = "rep", HeaderText = "REPRISE" }, 14);
            Fill(dg, DelCol(), 8);

            // non editables : N°, PLI et ✕ ; et tout sauf R sur la ligne "fin".
            // PLI est un AFFICHAGE : la ligne de pli d'une operation se choisit a la creation.
            dg.CellBeginEdit += (s, e) =>
            {
                string col = dg.Columns[e.ColumnIndex].Name;
                if (col == "ord" || col == "pli" || col == "del") { e.Cancel = true; return; }
                if (e.RowIndex == FinRow && col != "r") e.Cancel = true;
            };

            // Commit immediat UNIQUEMENT pour cases a cocher et listes : sur une cellule
            // texte, ce handler se declenche a chaque frappe et casse la saisie.
            dg.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (!dg.IsCurrentCellDirty) return;
                var c = dg.CurrentCell;
                if (c is DataGridViewCheckBoxCell || c is DataGridViewComboBoxCell)
                    dg.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            dg.CellValueChanged += (s, e) =>
            {
                if (_load || e.RowIndex < 0) return;
                ReadBack();
                RecalcHits();
                Edited?.Invoke();       // MainForm rafraichit l'AUTRE grille, pas celle-ci
            };

            // ✕ en bout de ligne
            dg.CellContentClick += (s, e) =>
            {
                if (_load || piece == null || e.RowIndex < 0 || e.RowIndex >= FinRow) return;
                if (dg.Columns[e.ColumnIndex].Name != "del") return;
                DeleteRowRequested?.Invoke(e.RowIndex);
            };

            dg.DataError += (s, e) => { e.ThrowException = false; };

            dg.SelectionChanged += (s, e) =>
            {
                if (_load || piece == null || dg.CurrentCell == null) return;
                int r = dg.CurrentCell.RowIndex;
                if (r >= 0 && r < piece.Sequence.Count && r != cur) StepPicked?.Invoke(r);
            };

            // ---- COULEURS : calculees au dessin, aucune mutation de style ----
            dg.CellFormatting += (s, e) =>
            {
                if (piece == null || e.RowIndex < 0 || e.ColumnIndex < 0) return;
                int n = piece.Sequence.Count;

                if (e.RowIndex >= n)     // ligne "fin"
                {
                    e.CellStyle.ForeColor = CFin; e.CellStyle.SelectionForeColor = CFin;
                    e.CellStyle.BackColor = CFinBg; e.CellStyle.SelectionBackColor = CFinBg;
                    return;
                }

                bool hit = e.RowIndex < hits.Length && hits[e.RowIndex];
                bool act = e.RowIndex == cur;
                Color bg = act ? CCurBg : CRow;
                Color fg = dg.Columns[e.ColumnIndex].Name == "del"
                    ? CRed
                    : (hit ? CRed : (act ? COrange : CGreen));

                e.CellStyle.ForeColor = fg;
                e.CellStyle.SelectionForeColor = fg;
                e.CellStyle.BackColor = bg;
                e.CellStyle.SelectionBackColor = act ? CCurBg : CSelBg;
            };

            Controls.Add(dg);
            Controls.Add(bar);
            Controls.Add(lblFoot);
            Controls.Add(lblTitle);
            dg.BringToFront();
        }

        DataGridViewButtonColumn DelCol()
        {
            var c = new DataGridViewButtonColumn
            {
                Name = "del", HeaderText = "", Text = "✕",
                UseColumnTextForButtonValue = true, FlatStyle = FlatStyle.Flat
            };
            c.DefaultCellStyle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            return c;
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
            var b = new Button { Text = t, Width = w, Height = 40, FlatStyle = FlatStyle.Flat,
                BackColor = CBtn, ForeColor = fg ?? CTxt, Margin = new Padding(4, 0, 4, 0),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
            b.FlatAppearance.BorderColor = CGrey;
            b.Click += (s, e) => onClick();
            return b;
        }

        // ---- changement de STRUCTURE : on reconstruit les lignes ----
        public void SetData(Piece p, int step, Poincon pn, Matrice mt, Embase eb)
        {
            piece = p; cur = step; poin = pn; mat = mt; emb = eb;
            Rebuild();
        }

        // ---- changement d'ETAPE seul : on recalcule les couleurs et on repeint ----
        public void SetStep(int step)
        {
            if (piece == null) return;
            cur = step;
            RecalcHits();
            dg.Invalidate();
        }

        void RecalcHits()
        {
            if (piece == null) { hits = new bool[0]; return; }
            var h = new bool[piece.Sequence.Count];
            for (int i = 0; i < h.Length; i++)
                h[i] = FoldEngine.Build(piece, i, cfg, poin, mat, emb).Collisions.Count > 0;
            hits = h;
        }

        string[] VStrings()
        {
            var l = new List<string>();
            if (mat != null) foreach (var vf in mat.Vs) l.Add(((int)vf.V).ToString());
            if (l.Count == 0) l.Add("16");
            return l.ToArray();
        }

        void Rebuild()
        {
            if (piece == null) return;
            _load = true;
            var mode = dg.EditMode;
            try
            {
                if (dg.IsCurrentCellInEditMode) dg.EndEdit();
                int cr = dg.CurrentCell?.RowIndex ?? -1, cc = dg.CurrentCell?.ColumnIndex ?? -1;

                // restaurer CurrentCell sans declencher une edition automatique
                dg.EditMode = DataGridViewEditMode.EditProgrammatically;

                var vcol = (DataGridViewComboBoxColumn)dg.Columns["v"];
                vcol.Items.Clear();
                foreach (var s in VStrings()) vcol.Items.Add(s);
                string v0 = VStrings()[0];

                dg.CurrentCell = null;
                dg.Rows.Clear();

                for (int i = 0; i < piece.Sequence.Count; i++)
                {
                    var o = piece.Sequence[i];
                    string vv = ((int)o.V).ToString();
                    if (!vcol.Items.Contains(vv)) vv = v0;
                    dg.Rows.Add(
                        (i + 1).ToString("00"),
                        "P" + (o.Bend + 1),
                        piece.ButeeInt(o.Bend).ToString("0.0", CultureInfo.InvariantCulture),
                        o.AngleCible.ToString("0", CultureInfo.InvariantCulture),
                        o.Sens == Sens.Haut ? "Haut" : "Bas",
                        vv,
                        o.Reprise);
                }

                // ligne FIN : le dernier pan, sans pli apres lui. Pas de bouton ✕ dessus.
                int fr = dg.Rows.Add("—", "fin", piece.ButeeInt(piece.NbPlis).ToString("0.0", CultureInfo.InvariantCulture), "", null, null, false);
                dg.Rows[fr].Cells["del"] = new DataGridViewTextBoxCell { Value = "" };

                if (cr >= 0 && cr < dg.Rows.Count && cc >= 0 && cc < dg.Columns.Count)
                    dg.CurrentCell = dg.Rows[cr].Cells[cc];

                string ep = piece.Epaisseur.ToString("0.##", CultureInfo.InvariantCulture);
                string smode = piece.CotesExterieures ? "extérieures (R converti en int.)" : "intérieures";
                lblFoot.Text = $"ép {ep} mm  ·  saisie {smode}  ·  R = cote intérieure lue à la butée arrière  ·  ✕ supprime la passe (et le pli si c'est la dernière)";
            }
            finally { dg.EditMode = mode; _load = false; }

            RecalcHits();
            dg.Invalidate();
        }

        // relit toute la grille dans la Piece (source unique de verite)
        void ReadBack()
        {
            if (piece == null) return;
            int n = piece.Sequence.Count;
            if (dg.Rows.Count < n + 1) return;
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

        // "P2" -> 2 ; "90°" -> 90 ; "40,5" -> 40.5
        static double ParseD(object o, double def)
        {
            if (o == null) return def;
            string t = o.ToString().Trim().Replace(',', '.').Replace("\u00b0", "").Replace("P", "").Replace("p", "");
            return double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;
        }
    }
}
