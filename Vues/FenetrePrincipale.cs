using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using SimulateurPliage.Materiel;
using SimulateurPliage.Pliage;

namespace SimulateurPliage.Vues
{
    public class FenetrePrincipale : Form
    {
        Atelier atelier;
        Plieuse plieuse;
        Poincon poincon;
        Matrice matrice;
        Piece piece = Piece.Demo();

        int etape;
        bool _load;

        ComboBox cbMachine, cbPoincon, cbMatrice, cbCotes;
        NumericUpDown nNbPlis, nEpaisseur, nHauteurPoincon;
        DataGridView dgPans;
        VueSection vueSection;
        VueDeveloppe vueDeveloppe;
        VuePupitre vuePupitre;
        TrackBar tbEtape;
        Label lblEtape, lblAlerte;
        RichTextBox rtSequence;
        Panel zoneDroite;

        const int LargeurPanneau = 300;

        public FenetrePrincipale()
        {
            Text = "Simulateur de pliage — collisions outillage · TolTem";
            Width = 1320; Height = 860;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Fond; ForeColor = Theme.Texte;
            Font = new Font("Segoe UI", 9);
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;

            atelier = Atelier.Charger();
            plieuse = atelier.Plieuses[0];
            poincon = atelier.Poincons[0];
            matrice = atelier.Matrices.Find(m => m.Nom.Contains("2045")) ?? atelier.Matrices[0];

            Construire();
            ChargerPans();
            Recalculer();
        }

        // ------------------------------------------------ construction --

        void Construire()
        {
            var racine = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Bord
            };
            racine.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 342));
            racine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(racine);

            var gauche = new Panel
            {
                Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Panneau,
                Padding = new Padding(14, 10, 14, 20), Margin = new Padding(0)
            };
            zoneDroite = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Fond, Margin = new Padding(0) };
            racine.Controls.Add(gauche, 0, 0);
            racine.Controls.Add(zoneDroite, 1, 0);

            int y = 4;
            y = Titre(gauche, "MACHINE", y, false);
            cbMachine = Combo(gauche, "Plieuse", Noms(atelier.Plieuses), 0, ref y, i =>
            {
                plieuse = atelier.Plieuses[i];
                vueSection.Outillage(plieuse, poincon, matrice, atelier.Embase);
                Recalculer();
            });

            y = Titre(gauche, "OUTILLAGE", y);
            cbPoincon = Combo(gauche, "Poinçon", Noms(atelier.Poincons), 0, ref y, i =>
            {
                poincon = atelier.Poincons[i];
                if (nHauteurPoincon != null) { _load = true; nHauteurPoincon.Value = (decimal)poincon.Hauteur; _load = false; }
                vueSection.Outillage(plieuse, poincon, matrice, atelier.Embase);
                Recalculer();
            });
            nHauteurPoincon = Num(gauche, "Hauteur poinçon", poincon.Hauteur, 60, 250, 5, 0, ref y, v =>
            {
                poincon.Hauteur = v;
                vueSection.Outillage(plieuse, poincon, matrice, atelier.Embase);
                Recalculer();
            });
            cbMatrice = Combo(gauche, "Matrice", Noms(atelier.Matrices),
                Math.Max(0, atelier.Matrices.IndexOf(matrice)), ref y, i =>
            {
                matrice = atelier.Matrices[i];
                vueSection.Outillage(plieuse, poincon, matrice, atelier.Embase);
                Recalculer();
            });

            y = Titre(gauche, "PIÈCE", y);
            nNbPlis = Num(gauche, "Nombre de plis", piece.NbPlis, 1, 12, 1, 0, ref y, v => DefinirNbPlis((int)v));
            nEpaisseur = Num(gauche, "Épaisseur (mm)", piece.Epaisseur, 0.4, 5, 0.1, 2, ref y,
                v => { piece.Epaisseur = v; Recalculer(); });
            cbCotes = Combo(gauche, "Cotes", new[] { "intérieures", "extérieures" }, 0, ref y,
                i => { piece.CotesExterieures = i == 1; Recalculer(); });

            y = Titre(gauche, "PANS (longueurs mm)", y);
            dgPans = Grille(gauche, 150, ref y);
            dgPans.Columns.Add(Col("pan", "Pan", 56, true));
            dgPans.Columns.Add(Col("lg", "Longueur", 150, false));
            dgPans.CellEndEdit += (s, e) => { if (!_load) { LirePans(); Recalculer(); } };

            y = Titre(gauche, "MACHINE — cotes", y);
            NumMachine(gauche, "Butée mini", plieuse.ButeeMin, ref y, v => plieuse.ButeeMin = v);
            NumMachine(gauche, "Butée maxi", plieuse.ButeeMax, ref y, v => plieuse.ButeeMax = v);
            NumMachine(gauche, "Hauteur libre", plieuse.HauteurLibre, ref y, v => plieuse.HauteurLibre = v);
            NumMachine(gauche, "Tablier déport", plieuse.TablierDeport, ref y, v => plieuse.TablierDeport = v);

            y = Titre(gauche, "EMBASES", y);
            NumMachine(gauche, "Porte-poinçon H", atelier.Embase.PortePoinconH, ref y, v => atelier.Embase.PortePoinconH = v);
            NumMachine(gauche, "Porte-poinçon L", atelier.Embase.PortePoinconLg, ref y, v => atelier.Embase.PortePoinconLg = v);
            NumMachine(gauche, "Semelle H", atelier.Embase.SemelleH, ref y, v => atelier.Embase.SemelleH = v);
            NumMachine(gauche, "Semelle L", atelier.Embase.SemelleLg, ref y, v => atelier.Embase.SemelleLg = v);

            ConstruireZoneDroite();
        }

        void ConstruireZoneDroite()
        {
            var bas = new Panel { Dock = DockStyle.Bottom, Height = 190, BackColor = Theme.Panneau };
            zoneDroite.Controls.Add(bas);

            lblAlerte = new Label
            {
                Dock = DockStyle.Top, Height = 30, ForeColor = Theme.Texte, BackColor = Theme.Panneau,
                Padding = new Padding(10, 6, 10, 0), Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            bas.Controls.Add(lblAlerte);

            rtSequence = new RichTextBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.Champ, ForeColor = Theme.Texte,
                BorderStyle = BorderStyle.None, ReadOnly = true, Font = new Font("Consolas", 9.5f)
            };
            bas.Controls.Add(rtSequence);
            rtSequence.BringToFront();

            var barre = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Theme.Fond };
            zoneDroite.Controls.Add(barre);

            var bPrec = Bouton("◀", 44, () => AllerEtape(etape - 1));
            bPrec.Left = 10; bPrec.Top = 9; bPrec.Height = 34; barre.Controls.Add(bPrec);
            var bSuiv = Bouton("▶", 44, () => AllerEtape(etape + 1));
            bSuiv.Left = 58; bSuiv.Top = 9; bSuiv.Height = 34; barre.Controls.Add(bSuiv);

            tbEtape = new TrackBar
            {
                Left = 110, Top = 8, Width = 215, Minimum = 0, Maximum = 1,
                TickStyle = TickStyle.None, BackColor = Theme.Fond
            };
            tbEtape.ValueChanged += (s, e) => { if (!_load) AllerEtape(tbEtape.Value); };
            barre.Controls.Add(tbEtape);

            lblEtape = new Label
            {
                Left = 335, Top = 14, Width = 420, ForeColor = Theme.Accent,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            barre.Controls.Add(lblEtape);

            var onglets = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, Width = 292, BackColor = Theme.Fond, Padding = new Padding(0, 9, 8, 0)
            };
            barre.Controls.Add(onglets);
            var b1 = Bouton("Section", 84, () => Vue(0)); b1.Height = 34;
            var b2 = Bouton("Développé", 92, () => Vue(1)); b2.Height = 34;
            var b3 = Bouton("Pupitre", 84, () => Vue(2)); b3.Height = 34;
            onglets.Controls.Add(b1); onglets.Controls.Add(b2); onglets.Controls.Add(b3);

            vueSection = new VueSection { Dock = DockStyle.Fill };
            vueDeveloppe = new VueDeveloppe { Dock = DockStyle.Fill, Visible = false };
            vuePupitre = new VuePupitre { Dock = DockStyle.Fill, Visible = false };
            zoneDroite.Controls.Add(vueSection);
            zoneDroite.Controls.Add(vueDeveloppe);
            zoneDroite.Controls.Add(vuePupitre);
            vueSection.BringToFront();
            vueSection.Outillage(plieuse, poincon, matrice, atelier.Embase);

            vuePupitre.Edited += () => { ChargerPans(); Recalculer(); };
            vuePupitre.StepPicked += r => AllerEtape(r);
            vuePupitre.AddBendRequested += AjouterPli;
            vuePupitre.AddOpRequested += AjouterEtape;
            vuePupitre.DelOpRequested += () => SupprimerEtape(vuePupitre.CurrentRow);
            vuePupitre.DeleteRowRequested += SupprimerEtape;
            vuePupitre.MoveOpRequested += DeplacerEtape;
            vuePupitre.SortRequested += TrierSequence;
            vuePupitre.AutoOrderRequested += OrdreAuto;
        }

        void Vue(int i)
        {
            vueSection.Visible = i == 0;
            vueDeveloppe.Visible = i == 1;
            vuePupitre.Visible = i == 2;
            if (i == 0) vueSection.BringToFront();
            else if (i == 1) vueDeveloppe.BringToFront();
            else vuePupitre.BringToFront();
        }

        // ------------------------------------------------- édition pièce --

        void DefinirNbPlis(int nb)
        {
            nb = Math.Max(1, nb);
            int pans = nb + 1;
            while (piece.Segments.Count < pans) piece.Segments.Add(100);
            while (piece.Segments.Count > pans) piece.Segments.RemoveAt(piece.Segments.Count - 1);
            piece.Sequence.RemoveAll(o => o.Bend >= piece.NbPlis);
            ChargerPans();
            Recalculer();
        }

        /// <summary>Ajoute une ligne de pli (un pan de plus) et l'opération qui va avec.</summary>
        void AjouterPli()
        {
            piece.Segments.Add(100);
            piece.Sequence.Add(new Operation
            {
                Bend = piece.NbPlis - 1, AngleCible = 90, Sens = Sens.Haut, V = VParDefaut()
            });
            ChargerPans();
            etape = piece.Sequence.Count - 1;
            Recalculer();
        }

        /// <summary>Ajoute une passe sur la première ligne libre ; en crée une si toutes sont prises.</summary>
        void AjouterEtape()
        {
            var utilisees = new HashSet<int>();
            foreach (var o in piece.Sequence) utilisees.Add(o.Bend);

            int bend = 0;
            while (utilisees.Contains(bend)) bend++;
            piece.AssurerPlis(bend + 1);

            piece.Sequence.Add(new Operation
            {
                Bend = bend, AngleCible = 90, Sens = Sens.Haut, V = VParDefaut()
            });
            ChargerPans();
            etape = piece.Sequence.Count - 1;
            Recalculer();
        }

        void SupprimerEtape(int idx)
        {
            if (idx < 0 || idx >= piece.Sequence.Count) return;
            piece.Sequence.RemoveAt(idx);
            etape = piece.Sequence.Count > 0 ? Math.Min(etape, piece.Sequence.Count - 1) : 0;
            ChargerPans();
            Recalculer();
        }

        void DeplacerEtape(int dir)
        {
            int i = vuePupitre.CurrentRow, j = i + dir;
            if (i < 0 || i >= piece.Sequence.Count || j < 0 || j >= piece.Sequence.Count) return;
            (piece.Sequence[i], piece.Sequence[j]) = (piece.Sequence[j], piece.Sequence[i]);
            etape = j;
            Recalculer();
        }

        void TrierSequence()
        {
            piece.Sequence.Sort((a, b) => a.Bend.CompareTo(b.Bend));
            etape = 0;
            Recalculer();
        }

        /// <summary>Cherche un ordre de pliage sans collision (recherche en profondeur).</summary>
        void OrdreAuto()
        {
            var ops = new List<Operation>(piece.Sequence);
            var ordre = new List<Operation>();
            var restant = new List<Operation>(ops);

            if (Explorer(ordre, restant))
            {
                piece.Sequence = ordre;
                etape = 0;
                Recalculer();
                MessageBox.Show("Ordre sans collision trouvé.", "Ordre auto",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                piece.Sequence = ops;
                Recalculer();
                MessageBox.Show("Aucun ordre sans collision trouvé.", "Ordre auto",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        bool Explorer(List<Operation> ordre, List<Operation> restant)
        {
            if (restant.Count == 0) return true;

            for (int i = 0; i < restant.Count; i++)
            {
                var op = restant[i];
                ordre.Add(op); restant.RemoveAt(i);

                var sauve = piece.Sequence;
                piece.Sequence = new List<Operation>(ordre);
                bool ok = !Moteur.Construire(piece, ordre.Count - 1, plieuse, poincon, matrice, atelier.Embase).Bloque;
                piece.Sequence = sauve;

                if (ok && Explorer(ordre, restant)) return true;

                restant.Insert(i, op); ordre.RemoveAt(ordre.Count - 1);
            }
            return false;
        }

        double VParDefaut()
        {
            if (piece.Sequence.Count > 0) return piece.Sequence[^1].V;
            return matrice != null && matrice.Vs.Count > 0 ? matrice.Vs[0].V : 16;
        }

        void ChargerPans()
        {
            _load = true;
            dgPans.Rows.Clear();
            for (int i = 0; i < piece.Segments.Count; i++)
                dgPans.Rows.Add((i + 1).ToString(),
                    piece.Segments[i].ToString("0.#", CultureInfo.InvariantCulture));
            if (nNbPlis != null)
                nNbPlis.Value = Math.Min(nNbPlis.Maximum, Math.Max(nNbPlis.Minimum, (decimal)piece.NbPlis));
            _load = false;
        }

        void LirePans()
        {
            for (int i = 0; i < dgPans.Rows.Count && i < piece.Segments.Count; i++)
                piece.Segments[i] = Lire(dgPans.Rows[i].Cells["lg"].Value, piece.Segments[i]);
        }

        // ---------------------------------------------------- affichage --

        void AllerEtape(int s)
        {
            etape = piece.Sequence.Count == 0 ? 0
                  : Math.Max(0, Math.Min(piece.Sequence.Count - 1, s));
            _load = true;
            tbEtape.Maximum = Math.Max(0, piece.Sequence.Count - 1);
            tbEtape.Value = Math.Min(tbEtape.Maximum, Math.Max(tbEtape.Minimum, etape));
            _load = false;
            Redessiner();
            vuePupitre.ChangerEtape(etape);
        }

        void Recalculer()
        {
            _load = true;
            tbEtape.Maximum = Math.Max(0, piece.Sequence.Count - 1);
            etape = Math.Max(0, Math.Min(tbEtape.Maximum, etape));
            tbEtape.Value = Math.Min(tbEtape.Maximum, Math.Max(tbEtape.Minimum, etape));
            _load = false;

            ListerSequence();
            Redessiner();
            vuePupitre.Afficher(piece, etape, plieuse, poincon, matrice, atelier.Embase);
        }

        void Redessiner()
        {
            var etat = Moteur.Construire(piece, etape, plieuse, poincon, matrice, atelier.Embase);
            vueSection.Afficher(etat, piece);
            vueDeveloppe.Afficher(piece, etape);

            if (piece.Sequence.Count == 0)
            {
                lblEtape.Text = "Aucune opération";
                lblAlerte.Text = "";
                return;
            }

            var op = piece.Sequence[etape];
            lblEtape.Text = $"Étape {etape + 1}/{piece.Sequence.Count}  ·  Pli {op.Bend + 1}  ·  " +
                            $"{piece.AngleAvant(etape):0}°→{op.AngleCible:0}°  ·  " +
                            $"{(op.Sens == Sens.Haut ? "Haut" : "Bas")}  ·  V{(int)op.V}";

            if (etat.Collisions.Count == 0)
            {
                lblAlerte.ForeColor = Theme.Reprise;
                lblAlerte.Text = "✔  Pas de collision à cette étape";
            }
            else
            {
                lblAlerte.ForeColor = Theme.Alerte;
                var parts = new List<string>();
                foreach (var c in etat.Collisions) parts.Add($"{c.Type} — {c.Detail}");
                lblAlerte.Text = "✖  " + string.Join("   |   ", parts);
            }
        }

        void ListerSequence()
        {
            rtSequence.Clear();
            var flips = piece.Retournements();

            for (int i = 0; i < piece.Sequence.Count; i++)
            {
                var o = piece.Sequence[i];
                var etat = Moteur.Construire(piece, i, plieuse, poincon, matrice, atelier.Embase);
                bool hit = etat.Collisions.Count > 0;
                bool flip = i < flips.Length && flips[i];

                Color col = hit ? Theme.Alerte : (o.Reprise ? Theme.Reprise : Theme.Tole);
                string etat_ = hit ? $"COLLISION: {etat.Collisions[0].Type}"
                             : (o.Reprise ? "reprise" : "direct");
                if (flip) etat_ += "  ⟲ retourner";

                rtSequence.SelectionStart = rtSequence.TextLength;
                rtSequence.SelectionColor = i == etape ? Theme.Accent : col;
                rtSequence.AppendText(string.Format(CultureInfo.InvariantCulture,
                    "{0,2}.  Pli {1,-2}  {2,3:0}°→{3,3:0}°  {4,-4}  V{5,-2}  · butée {6,4:0}  · {7}\n",
                    i + 1, o.Bend + 1, piece.AngleAvant(i), o.AngleCible,
                    o.Sens == Sens.Haut ? "Haut" : "Bas", (int)o.V, etat.ButeeDistance, etat_));
            }
        }

        // ------------------------------------------------------ widgets --

        static string[] Noms<T>(List<T> liste)
        {
            var a = new string[liste.Count];
            for (int i = 0; i < a.Length; i++) a[i] = liste[i].ToString();
            return a;
        }

        int Titre(Panel p, string t, int y, bool separateur = true)
        {
            if (separateur)
            {
                p.Controls.Add(new Panel
                {
                    Left = 10, Top = y + 6, Width = LargeurPanneau + 10, Height = 1, BackColor = Theme.Separateur
                });
                y += 12;
            }
            p.Controls.Add(new Label
            {
                Text = t, Left = 10, Top = y + 6, Width = LargeurPanneau,
                ForeColor = Theme.Accent, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            });
            return y + 30;
        }

        NumericUpDown Num(Panel p, string lab, double v, double min, double max,
                          double inc, int dec, ref int y, Action<double> onChange)
        {
            p.Controls.Add(new Label { Text = lab, Left = 12, Top = y + 4, Width = 170, ForeColor = Theme.Texte });
            var n = new NumericUpDown
            {
                Left = 190, Top = y, Width = 148,
                Minimum = (decimal)min, Maximum = (decimal)max, Increment = (decimal)inc,
                DecimalPlaces = dec, Value = (decimal)Math.Max(min, Math.Min(max, v)),
                BackColor = Theme.Champ, ForeColor = Theme.Texte, BorderStyle = BorderStyle.FixedSingle
            };
            n.ValueChanged += (s, e) => { if (!_load) onChange((double)n.Value); };
            p.Controls.Add(n);
            y += 30;
            return n;
        }

        void NumMachine(Panel p, string lab, double v, ref int y, Action<double> onChange)
        {
            p.Controls.Add(new Label { Text = lab, Left = 20, Top = y + 4, Width = 168, ForeColor = Theme.Discret });
            var n = new NumericUpDown
            {
                Left = 190, Top = y, Width = 148, Minimum = 0, Maximum = 5000,
                DecimalPlaces = 1, Increment = 1, Value = (decimal)v,
                BackColor = Theme.Champ, ForeColor = Theme.Texte, BorderStyle = BorderStyle.FixedSingle
            };
            n.ValueChanged += (s, e) => { if (!_load) { onChange((double)n.Value); Recalculer(); } };
            p.Controls.Add(n);
            y += 28;
        }

        ComboBox Combo(Panel p, string lab, string[] items, int sel, ref int y, Action<int> onChange)
        {
            p.Controls.Add(new Label { Text = lab, Left = 12, Top = y + 4, Width = 170, ForeColor = Theme.Texte });
            var c = new ComboBox
            {
                Left = 190, Top = y, Width = 148, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.Champ, ForeColor = Theme.Texte, FlatStyle = FlatStyle.Flat
            };
            c.Items.AddRange(items);
            if (items.Length > 0) c.SelectedIndex = Math.Max(0, Math.Min(items.Length - 1, sel));
            c.SelectedIndexChanged += (s, e) => { if (!_load) onChange(c.SelectedIndex); };
            p.Controls.Add(c);
            y += 30;
            return c;
        }

        DataGridView Grille(Panel p, int h, ref int y)
        {
            var g = new DataGridView
            {
                Left = 12, Top = y, Width = LargeurPanneau + 10, Height = h,
                BackgroundColor = Theme.Champ, BorderStyle = BorderStyle.None, GridColor = Theme.Bord,
                RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false, EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect
            };
            g.DefaultCellStyle.BackColor = Theme.Champ;
            g.DefaultCellStyle.ForeColor = Theme.Texte;
            g.DefaultCellStyle.SelectionBackColor = Theme.Bouton;
            g.DefaultCellStyle.SelectionForeColor = Theme.Texte;
            g.ColumnHeadersDefaultCellStyle.BackColor = Theme.Panneau;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Discret;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 26;
            p.Controls.Add(g);
            y += h + 8;
            return g;
        }

        static DataGridViewTextBoxColumn Col(string nom, string entete, int w, bool lectureSeule)
            => new() { Name = nom, HeaderText = entete, Width = w, ReadOnly = lectureSeule };

        Button Bouton(string t, int w, Action onClick)
        {
            var b = new Button
            {
                Text = t, Width = w, Height = 28, FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Bouton, ForeColor = Theme.Texte, Margin = new Padding(2)
            };
            b.FlatAppearance.BorderColor = Theme.Bord;
            b.Click += (s, e) => onClick();
            return b;
        }

        static double Lire(object o, double defaut)
        {
            if (o == null) return defaut;
            string t = o.ToString().Trim().Replace(',', '.');
            return double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : defaut;
        }
    }
}
