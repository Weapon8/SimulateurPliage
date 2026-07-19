using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using SimulateurPliage.Materiel;
using SimulateurPliage.Pliage;

namespace SimulateurPliage.Vues
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
    public class VuePupitre : Panel
    {
        public event Action Edited;                 // une valeur a change
        public event Action<int> StepPicked;        // l'operateur a clique une ligne
        public event Action AddBendRequested;       // + pli
        public event Action AddOpRequested;         // + étape (2e passe -> reprise)
        public event Action DelOpRequested;         // supprime la passe courante
        public event Action SortRequested;          // remet le programme dans l'ordre des plis
        public event Action AutoOrderRequested;     // cherche un ordre de pliage sans collision
        public event Action<int> DeleteRowRequested;// ✕ : supprime la passe (et le pli si derniere)
        public event Action<int> MoveOpRequested;

        Plieuse plieuse;
        Piece piece;
        int cur = -1;
        Poincon poincon;
        Matrice matrice;
        Embase embase;
        bool _load;
        bool[] hits = new bool[0];   // collision par operation, recalcule a chaque etape
        int[] surBends = new int[0]; // plis (index geometrique) a surligner = ceux bordant le pan sélectionné

        DataGridView dg;
        Label lblTitle, lblFoot, lblAlerte;

        static readonly Color CBg    = Color.FromArgb(12, 15, 20);
        static readonly Color CRow   = Color.FromArgb(22, 27, 34);
        static readonly Color CCurBg = Color.FromArgb(40, 30, 12);
        static readonly Color CSelBg = Color.FromArgb(34, 42, 52);
        static readonly Color CSurBg = Color.FromArgb(30, 52, 74);   // pli bordant le pan sélectionné
        static readonly Color CFinBg = Color.FromArgb(18, 22, 28);
        static readonly Color CHead  = Color.FromArgb(140, 150, 162);
        static readonly Color CGreen = Color.FromArgb(80, 230, 120);
        static readonly Color COrange= Color.FromArgb(255, 170, 60);
        static readonly Color CRed   = Color.FromArgb(235, 90, 80);
        static readonly Color CGrey  = Color.FromArgb(50, 58, 68);
        static readonly Color CBtn   = Color.FromArgb(43, 49, 59);
        static readonly Color CTxt   = Color.FromArgb(230, 235, 240);
        static readonly Color CFin   = Color.FromArgb(120, 130, 145);
        static readonly Color CBlue  = Color.FromArgb(111, 208, 255);  // ⇄ à plat
        static readonly Color CAmber = Color.FromArgb(255, 184, 77);   // ⇅ dessus/dessous
        static readonly Color CDim   = Color.FromArgb(66, 74, 84);     // sigle éteint

        public int CurrentRow => dg?.CurrentCell?.RowIndex ?? -1;

        int FinRow => piece != null ? piece.Sequence.Count : -1;

        public VuePupitre()
        {
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

            // Bannière d'alerte « pli n°1 » : rouge, cachée par défaut. Apparaît quand la
            // 1re étape n'est PAS le pli le plus fermé — règle métier dure (fermé d'abord).
            lblAlerte = new Label
            {
                Dock = DockStyle.Top, Height = 0, Visible = false,
                ForeColor = Color.White, BackColor = Color.FromArgb(176, 32, 32),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0)
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
            bar.Controls.Add(Btn("Ordre auto", 150, () => AutoOrderRequested?.Invoke(), COrange));

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
            Fill(dg, ComboCol("v", "V", new[] { "16" }), 10);
            Fill(dg, new DataGridViewCheckBoxColumn { Name = "inv", HeaderText = "⇄ À PLAT" }, 12);
            Fill(dg, new DataGridViewCheckBoxColumn { Name = "ret", HeaderText = "⇅ FACE" }, 11);
            Fill(dg, new DataGridViewCheckBoxColumn { Name = "rep", HeaderText = "REPRISE" }, 11);
            Fill(dg, DelCol(), 8);

            // non editables : N°, PLI et ✕ ; et tout sauf R sur la ligne "fin".
            // PLI est un AFFICHAGE : la ligne de pli d'une operation se choisit a la creation.
            dg.CellBeginEdit += (s, e) =>
            {
                string col = dg.Columns[e.ColumnIndex].Name;
                if (col == "ord" || col == "pli" || col == "del" || col == "rep") { e.Cancel = true; return; }
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
                UpdateFoot();
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
                bool sur = EstBordant(e.RowIndex);
                Color bg = act ? CCurBg : (sur ? CSurBg : CRow);
                Color fg = dg.Columns[e.ColumnIndex].Name == "del"
                    ? CRed
                    : (hit ? CRed : (act ? COrange : (sur ? CTxt : CGreen)));

                e.CellStyle.ForeColor = fg;
                e.CellStyle.SelectionForeColor = fg;
                e.CellStyle.BackColor = bg;
                e.CellStyle.SelectionBackColor = act ? CCurBg : (sur ? CSurBg : CSelBg);
            };

            // ---- SIGLES ⇄ / ⇅ : on redessine les cases inv/ret en flèches ----
            // Les cellules restent des cases à cocher (le clic bascule la valeur sur
            // toute la cellule) ; on remplace juste le visuel. Éteint = gris discret,
            // coché = couleur vive. On repeint fond + bordure via e.Paint puis le sigle.
            dg.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                string col = dg.Columns[e.ColumnIndex].Name;
                if (col != "inv" && col != "ret") return;

                e.Paint(e.CellBounds, DataGridViewPaintParts.Background
                                    | DataGridViewPaintParts.SelectionBackground
                                    | DataGridViewPaintParts.Border);

                // ligne "fin" : pas d'opération -> pas de sigle
                if (piece != null && e.RowIndex < piece.Sequence.Count)
                {
                    bool on = dg.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is bool b && b;
                    bool horiz = col == "inv";
                    Color c = on ? (horiz ? CBlue : CAmber) : CDim;
                    DessinerSigle(e.Graphics, e.CellBounds, horiz, c);
                }

                e.Handled = true;
            };

            Controls.Add(dg);
            Controls.Add(bar);
            Controls.Add(lblFoot);
            Controls.Add(lblAlerte);   // ajouté après title -> s'empile sous le titre
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

        // Surligne les plis (opérations) dont le pli géométrique est l'un de ceux passés
        // — appelé quand l'opérateur clique un pan pour montrer les 2 plis qui le bordent.
        public void SurlignerPlis(params int[] plisGeo)
        {
            surBends = plisGeo ?? new int[0];
            dg?.Invalidate();
        }

        bool EstBordant(int row)
        {
            if (piece == null || surBends.Length == 0 ||
                row < 0 || row >= piece.Sequence.Count) return false;
            int b = piece.Sequence[row].Bend;
            foreach (int x in surBends) if (x == b) return true;
            return false;
        }

        // place le curseur sur une ligne sans ouvrir d'editeur
        public void SelectRow(int r)
        {
            if (dg == null || r < 0 || r >= dg.Rows.Count) return;
            _load = true;
            var mode = dg.EditMode;
            dg.EditMode = DataGridViewEditMode.EditProgrammatically;
            dg.CurrentCell = dg.Rows[r].Cells["r"];
            dg.EditMode = mode;
            _load = false;
        }

        // ---- changement de STRUCTURE : on reconstruit les lignes ----
        public void Afficher(Piece p, int step, Plieuse pl, Poincon po, Matrice ma, Embase em)
        {
            piece = p; cur = step; plieuse = pl; poincon = po; matrice = ma; embase = em;
            Rebuild();
        }

        // ---- changement d'ETAPE seul : on recalcule les couleurs et on repeint ----
        public void ChangerEtape(int step)
        {
            if (piece == null) return;
            cur = step;
            RecalcHits();
            dg.Invalidate();
        }

        /// <summary>
        /// Vérifie la règle « pli fermé en premier » et affiche/masque la bannière d'alerte.
        /// Le 1er pli formé DOIT être le plus fermé (angle le plus petit) : sinon la tôle se
        /// rigidifie et on ne peut plus le former, ou le rebord tape le tablier. Si l'opérateur
        /// a mis un autre pli en tête, on l'alerte en rouge — sans bloquer (il reste maître).
        /// </summary>
        void MajAlertePremierPli()
        {
            if (lblAlerte == null) return;
            if (piece == null || piece.Sequence.Count == 0)
            { lblAlerte.Visible = false; lblAlerte.Height = 0; return; }

            piece.AssurerForme();
            // angle le plus fermé de toute la pièce
            double mini = double.MaxValue;
            foreach (var an in piece.Angles) mini = Math.Min(mini, an);
            double premier = piece.Sequence[0].AngleCible;

            // L'alerte n'a de sens QUE si la pièce contient un pli franchement AIGU (≤ 45°) :
            // un raidisseur, une pince. C'est LUI qui doit passer en premier (il rigidifie la
            // tôle). Sans pli aigu (profil tout à 90°, ou 90/92…), aucun pli n'est critique :
            // pas d'alerte, quel que soit l'ordre. SEUIL_AIGU = 45° (règle Weapon).
            const double SEUIL_AIGU = 45.0;
            bool aUnAigu = mini <= SEUIL_AIGU + 1.0;                 // existe-t-il un pli ≤ 45° ?
            bool premierEstLAigu = premier <= SEUIL_AIGU + 1.0;      // l'étape 1 est-elle cet aigu ?

            // On alerte seulement si un pli aigu existe ET qu'il n'est pas en tête.
            if (!aUnAigu || premierEstLAigu)
            { lblAlerte.Visible = false; lblAlerte.Height = 0; }
            else
            {
                lblAlerte.Text = $"⚠ ATTENTION — un pli fermé à {mini:0}° doit être formé EN PREMIER, "
                    + $"or l'étape 1 plie à {premier:0}°. "
                    + "Le pli aigu rigidifie la tôle : formé après, il ne passe plus ou tape le tablier.";
                lblAlerte.Height = 44;
                lblAlerte.Visible = true;
            }
        }

        void UpdateFoot()
        {
            MajAlertePremierPli();
            if (piece == null) return;
            double dev = 0; foreach (var v in piece.Segments) dev += v;
            string ep = piece.Epaisseur.ToString("0.##", CultureInfo.InvariantCulture);
            string smode = piece.CotesExterieures ? "extérieures" : "intérieures";
            lblFoot.Text = $"L développé {dev:0.#} mm  ·  {piece.Segments.Count} pans  ·  ép {ep} mm  ·  cotes {smode}  ·  " +
                           "R = cote lue à la butée  ·  ⇄ = rotation 180° à plat  ·  ⇅ = retournée dessus/dessous  ·  REPRISE = auto (pli déjà formé)  ·  ✕ supprime la passe";
        }

        void RecalcHits()
        {
            if (piece == null) { hits = new bool[0]; return; }
            var h = new bool[piece.Sequence.Count];
            for (int i = 0; i < h.Length; i++)
                h[i] = Moteur.Construire(piece, i, plieuse, poincon, matrice, embase).Collisions.Count > 0;
            hits = h;
        }

        string[] VStrings()
        {
            var l = new List<string>();
            if (matrice != null) foreach (var vf in matrice.Vs) l.Add(((int)vf.V).ToString());
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
                    int bi = o.ButeeAval ? o.Bend + 1 : o.Bend;
                    dg.Rows.Add(
                        (i + 1).ToString("00"),
                        "P" + (o.Bend + 1),
                        piece.ButeeInt(bi).ToString("0.0", CultureInfo.InvariantCulture),
                        o.AngleCible.ToString("0", CultureInfo.InvariantCulture),
                        o.Sens == Sens.Haut ? "Haut" : "Bas",
                        vv,
                        o.ButeeAval,
                        o.Retournee,
                        o.Reprise);
                }

                // ligne FIN : le dernier pan, sans pli apres lui. Pas de bouton ✕ dessus.
                int fr = dg.Rows.Add("—", "fin", piece.ButeeInt(piece.NbPlis).ToString("0.0", CultureInfo.InvariantCulture), "", null, null, false, false, false);
                dg.Rows[fr].Cells["del"] = new DataGridViewTextBoxCell { Value = "" };

                if (cr >= 0 && cr < dg.Rows.Count && cc >= 0 && cc < dg.Columns.Count)
                    dg.CurrentCell = dg.Rows[cr].Cells[cc];

                UpdateFoot();
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
                bool aval = row.Cells["inv"].Value is bool ba && ba;

                // la cote R saisie porte sur le pan effectivement lu par la butee
                int bi = Math.Min(piece.Segments.Count - 1, aval ? bend + 1 : bend);
                piece.SetButeeInt(bi, ParseD(row.Cells["r"].Value, piece.ButeeInt(bi)));

                list.Add(new Operation
                {
                    Bend = bend,
                    AngleCible = Math.Max(1, Math.Min(179, ParseD(row.Cells["ang"].Value, 90))),
                    Sens = (row.Cells["sens"].Value as string) == "Bas" ? Sens.Bas : Sens.Haut,
                    V = ParseD(row.Cells["v"].Value, 16),
                    ButeeAval = aval,
                    Retournee = row.Cells["ret"].Value is bool br && br,
                    Reprise = false   // déduit après coup par NormaliserReprises
                });
            }
            piece.Sequence = list;
            piece.NormaliserReprises();
        }

        // "P2" -> 2 ; "90°" -> 90 ; "40,5" -> 40.5
        static double ParseD(object o, double def)
        {
            if (o == null) return def;
            string t = o.ToString().Trim().Replace(',', '.').Replace("\u00b0", "").Replace("P", "").Replace("p", "");
            return double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        // Dessine ⇄ (2 flèches horizontales) ou ⇅ (2 flèches verticales inversées),
        // centré dans la cellule. Tracé au trait -> net, sans dépendre d'une police.
        static void DessinerSigle(Graphics g, Rectangle cell, bool horizontal, Color col)
        {
            var sm = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(col, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cx = cell.X + cell.Width / 2f;
                float cy = cell.Y + cell.Height / 2f;
                const float h = 9f, o = 4f;   // demi-longueur, écart entre les 2 flèches
                if (horizontal)               // ⇄ : haut vers la droite, bas vers la gauche
                {
                    Fleche(g, p, cx - h, cy - o, cx + h, cy - o);
                    Fleche(g, p, cx + h, cy + o, cx - h, cy + o);
                }
                else                          // ⇅ : gauche vers le haut, droite vers le bas
                {
                    Fleche(g, p, cx - o, cy + h, cx - o, cy - h);
                    Fleche(g, p, cx + o, cy - h, cx + o, cy + h);
                }
            }
            g.SmoothingMode = sm;
        }

        // segment + pointe de flèche à l'extrémité (x2,y2)
        static void Fleche(Graphics g, Pen p, float x1, float y1, float x2, float y2)
        {
            g.DrawLine(p, x1, y1, x2, y2);
            double a = Math.Atan2(y2 - y1, x2 - x1);
            const float L = 4.5f;
            for (int k = -1; k <= 1; k += 2)
            {
                double b = a + Math.PI + k * (Math.PI / 6);   // ±30°
                g.DrawLine(p, x2, y2, x2 + (float)(L * Math.Cos(b)), y2 + (float)(L * Math.Sin(b)));
            }
        }
    }
}
