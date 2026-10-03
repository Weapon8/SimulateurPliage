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
        Bibliotheque biblio;
        Plieuse plieuse;
        Poincon poincon;
        Matrice matrice;
        Piece piece = Piece.DemoZLaque();   // démo active : Z laqué (chevêtre = Piece.Demo())

        int etape;
        bool _load;
        string _fichier;   // chemin du .plt.json courant ; null = pièce jamais enregistrée

        // Verrou des réglages machine : verrouillés par défaut, on doit déverrouiller
        // (🔓) pour éditer, puis Valider pour appliquer et enregistrer dans l'atelier.
        bool _machVerrouille = true;
        bool _machModifie;
        readonly System.Collections.Generic.List<NumericUpDown> _champsMachine = new();
        // lecture de la cote affichée par chaque champ machine, pour les recharger quand on
        // change de plieuse (sinon ils gardent les cotes de la machine précédente)
        readonly System.Collections.Generic.List<Func<double>> _lecturesMachine = new();
        Button btnVerrou, btnValiderMachine;
        Label lblMachModifie;
        Panel machPanel;          // bloc machine repliable (en bas)
        Button btnMachHead;       // en-tête ▸/▾ du bloc machine

        ComboBox cbMachine, cbPoincon, cbMatrice, cbCotes, cbProfils, cbMatiere;
        TextBox txtNom, txtChantier, txtPans;
        readonly System.Collections.Generic.List<Profil> _profils = new();
        NumericUpDown nNbPlis, nEpaisseur, nHauteurPoincon, nLongPli;
        DataGridView dgPans;
        int _ligneSurl = -1;   // ligne de la grille PLIS à surligner = l'étape affichée

        // Matières proposées : Rm en N/mm² (les valeurs de Piece.Rm). Sert au calcul de tonnage.
        static readonly string[] MatiereNoms = { "Acier · Rm 450", "Inox · Rm 600", "Alu · Rm 250", "Zinc · Rm 150" };
        static readonly double[] MatiereRm   = { 450, 600, 250, 150 };
        VueSection vueSection;
        VueDeveloppe vueDeveloppe;
        VuePupitre vuePupitre;
        VueVolume vue3D;
        TrackBar tbEtape;
        Button ongletPupitre, ongletSection, ongletDeveloppe, onglet3D;
        Label lblEtape, lblAlerte;
        RichTextBox rtSequence;
        Panel zoneDroite;

        const int LargeurPanneau = 300;

        public FenetrePrincipale()
        {
            Text = "Simulateur de pliage — collisions outillage · TolTem";
            // AutoScaleDimensions AVANT Width/Height (règle des outils TolTem).
            Font = new Font("Segoe UI", 9);
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Width = 1320; Height = 860;
            MinimumSize = new Size(1000, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Fond; ForeColor = Theme.Texte;
            // L'icône de la fenêtre = celle de l'exe (toltem.ico, posée par le .csproj).
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            atelier = Atelier.Charger();
            biblio = Bibliotheque.Charger();
            plieuse = atelier.Plieuses[0];
            poincon = atelier.Poincons[0];
            matrice = atelier.Matrices.Find(m => m.Nom.Contains("2045")) ?? atelier.Matrices[0];

            Construire();
            ChargerPans();
            Recalculer();   // on garde l'ordre de la démo (séquence opérateur figée)
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
            gauche.Paint += (snd, pe) =>
            {
                using var pen = new Pen(Theme.Separateur, 1);
                pe.Graphics.DrawLine(pen, gauche.Width - 1, 0, gauche.Width - 1, gauche.Height);
            };
            racine.Controls.Add(gauche, 0, 0);
            racine.Controls.Add(zoneDroite, 1, 0);

            int y = 4;

            // --- EN TÊTE : on choisit la machine et l'outillage AVANT de simuler ---
            y = Titre(gauche, "MACHINE", y, false);
            cbMachine = Combo(gauche, "Plieuse", Noms(atelier.Plieuses), 0, ref y, i =>
            {
                plieuse = atelier.Plieuses[i];
                RechargerChampsMachine();
                vueSection.Outillage(plieuse, poincon, matrice, atelier.Embase);
                vue3D.Outillage(plieuse, poincon, matrice, atelier.Embase);
                Recalculer();
            });

            y = Titre(gauche, "OUTILLAGE", y);
            cbPoincon = Combo(gauche, "Poinçon", Noms(atelier.Poincons), 0, ref y, i =>
            {
                poincon = atelier.Poincons[i];
                if (nHauteurPoincon != null) { _load = true; nHauteurPoincon.Value = (decimal)poincon.Hauteur; _load = false; }
                vueSection.Outillage(plieuse, poincon, matrice, atelier.Embase);
                vue3D.Outillage(plieuse, poincon, matrice, atelier.Embase);
                Recalculer();
            });
            cbMatrice = Combo(gauche, "Matrice", Noms(atelier.Matrices),
                Math.Max(0, atelier.Matrices.IndexOf(matrice)), ref y, i =>
            {
                matrice = atelier.Matrices[i];
                vueSection.Outillage(plieuse, poincon, matrice, atelier.Embase);
                vue3D.Outillage(plieuse, poincon, matrice, atelier.Embase);
                Recalculer();
            });

            // --- PIÈCE + PANS ---
            y = Titre(gauche, "PIÈCE", y);
            nNbPlis = Num(gauche, "Nombre de plis", piece.NbPlis, 1, 12, 1, 0, ref y, v => DefinirNbPlis((int)v));
            nEpaisseur = Num(gauche, "Épaisseur (mm)", piece.Epaisseur, 0.4, 5, 0.1, 2, ref y,
                v => { piece.Epaisseur = v; Recalculer(); });
            cbCotes = Combo(gauche, "Cotes", new[] { "intérieures", "extérieures" }, 0, ref y,
                i => { piece.CotesExterieures = i == 1; Recalculer(); });

            // LES PANS, dans l'ordre du profil — comme on nomme une pièce à l'atelier :
            // « 30 40 150 200 100 10 ». C'est le seul endroit où TOUS les pans se saisissent.
            // La colonne R du pupitre ne montre que le pan calé en butée à chaque étape : selon
            // la gamme, un pan n'y apparaît jamais (le 150 du chéneau) et ne pouvait donc être
            // corrigé nulle part. Entrée ou sortie du champ = validé ; Échap = on annule.
            gauche.Controls.Add(new Label { Text = "Pans (mm)", Left = 16, Top = y + 4, Width = 80, ForeColor = Theme.Texte });
            txtPans = new TextBox
            {
                Left = 100, Top = y, Width = 238,
                BackColor = Theme.Champ, ForeColor = Theme.Texte, BorderStyle = BorderStyle.FixedSingle
            };
            txtPans.KeyDown += (snd, ke) =>
            {
                if (ke.KeyCode == Keys.Enter) { ke.SuppressKeyPress = true; ValiderPans(true); }
                else if (ke.KeyCode == Keys.Escape) { ke.SuppressKeyPress = true; AfficherPans(); }
            };
            txtPans.Leave += (snd, le) => ValiderPans(false);
            new ToolTip().SetToolTip(txtPans,
                "Les pans dans l'ordre du profil, séparés par un espace : 30 40 150 200 100 10\nEntrée = valider · Échap = annuler");
            gauche.Controls.Add(txtPans);
            y += 30;

            // Longueur de pli et matière : le tonnage (machine et t/m du poinçon) et le contrôle
            // 100–4050 mm en dépendent. Sans ces deux champs ils tournaient toujours sur 500 mm
            // d'acier, quelle que soit la pièce.
            nLongPli = Num(gauche, "Longueur de pli (mm)", piece.LongueurPli, 10, 6000, 50, 0, ref y,
                v => { piece.LongueurPli = v; Recalculer(); });
            cbMatiere = Combo(gauche, "Matière", MatiereNoms, IndexMatiere(piece.Rm), ref y,
                i => { if (i >= 0 && i < MatiereRm.Length) { piece.Rm = MatiereRm[i]; Recalculer(); } });

            y = Titre(gauche, "PLIS", y);
            // RAPPEL, PAS SAISIE. Tout se tape au pupitre — c'est lui la CN. Cette grille
            // n'est là que pour avoir la longueur, l'angle et la face CÔTE À CÔTE sous les yeux :
            // personne ne pourra dire « j'avais pas vu ». Elle se met à jour toute seule à chaque
            // édition du pupitre et après un ordre auto (vuePupitre.Edited -> ChargerPans).
            // Longueur et angle sont en lecture seule : deux endroits pour saisir la même cote,
            // c'est deux endroits pour se tromper. Seule la Face se bascule ici, d'un clic.
            dgPans = Grille(gauche, 150, ref y);
            dgPans.Columns.Add(Col("pli", "Pli", 40, true));
            dgPans.Columns.Add(Col("lg", "Longueur", 78, true));
            dgPans.Columns.Add(Col("ang", "Angle", 56, true));
            dgPans.Columns.Add(Col("face", "Face", 56, true));
            dgPans.ReadOnly = true;
            // La colonne Face est la SEULE éditable : un clic bascule FNL <-> FL.
            // FL = côté brillant / laqué / galva-zinc traité, va vers le visible du produit.
            // FNL = l'autre côté. C'est une donnée de la PIÈCE (elle vient du dessin), pas de
            // la séquence : le solveur en déduit les retournements, jamais l'inverse.

            // --- SYNCHRO PANS ↔ PUPITRE ---
            // coloriage : les pans qui bordent le pli sélectionné dans le pupitre
            dgPans.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                bool sur = e.RowIndex == _ligneSurl;
                e.CellStyle.BackColor = sur ? Color.FromArgb(30, 52, 74) : Theme.Champ;
                e.CellStyle.SelectionBackColor = sur ? Color.FromArgb(38, 62, 86) : Color.FromArgb(48, 56, 68);
                e.CellStyle.ForeColor = sur ? Color.White : Theme.Texte;

                // La face reprend le code couleur de la vue section : bleu = FNL, violet = FL.
                // Le dernier pan n'a pas de pli après lui : gris.
                string col = dgPans.Columns[e.ColumnIndex].Name;
                string val = e.Value as string;
                if (col == "face")
                    e.CellStyle.ForeColor = val == "FL" ? Theme.ToleFL
                                          : val == "FNL" ? Theme.Tole : Theme.Discret;
                else if ((col == "ang" || col == "lg") && val == "—")
                    e.CellStyle.ForeColor = Theme.Discret;
            };
            // clic sur un pan -> surligne les 2 plis qui le bordent (pli p-1 et pli p)
            dgPans.SelectionChanged += (s, e) =>
            {
                if (_load || vuePupitre == null) return;
                int p = dgPans.CurrentCell?.RowIndex ?? -1;
                if (p < 0 || p >= piece.Sequence.Count) { vuePupitre.SurlignerPlis(); return; }
                vuePupitre.SurlignerPlis(piece.Sequence[p].Bend);   // surligne le pli de cette étape
            };
            // clic sur la colonne FACE -> bascule FNL <-> FL pour ce pli, et re-solve.
            dgPans.CellClick += (s, e) => BasculerFace(e.RowIndex, e.ColumnIndex);
            // curseur main quand on survole une case Face éditable (pli existant)
            dgPans.CellMouseMove += (s, e) =>
            {
                bool surFace = e.RowIndex >= 0 && e.ColumnIndex >= 0
                    && dgPans.Columns[e.ColumnIndex].Name == "face"
                    && e.RowIndex < piece.Sequence.Count;
                dgPans.Cursor = surFace ? Cursors.Hand : Cursors.Default;
            };

            // --- AU MILIEU : fichier + bibliothèque de profils ---
            y = Titre(gauche, "FICHIER", y);
            var bNouveau = Bouton("Nouveau", 96, NouvellePiece);
            bNouveau.Left = 16; bNouveau.Top = y; gauche.Controls.Add(bNouveau);
            var bOuvrir = Bouton("Ouvrir", 96, OuvrirPiece);
            bOuvrir.Left = 116; bOuvrir.Top = y; gauche.Controls.Add(bOuvrir);
            var bEnreg = Bouton("Enregistrer", 118, EnregistrerPiece);
            bEnreg.Left = 216; bEnreg.Top = y; gauche.Controls.Add(bEnreg);
            y += 34;
            var bEnregSous = Bouton("Enregistrer sous…", 196, EnregistrerPieceSous);
            bEnregSous.Left = 16; bEnregSous.Top = y; gauche.Controls.Add(bEnregSous);
            // Autotest : les règles figées, contrôlées d'un clic dans l'exe (plus seulement au banc).
            var bAutotest = Bouton("Autotest", 118, LancerAutotest);
            bAutotest.Left = 216; bAutotest.Top = y; gauche.Controls.Add(bAutotest);
            y += 38;

            y = Titre(gauche, "PROFILS", y);
            txtNom = Texte(gauche, "Nom", piece.Nom, ref y, s => piece.Nom = s);
            txtChantier = Texte(gauche, "Chantier", piece.Chantier, ref y, s => piece.Chantier = s);
            gauche.Controls.Add(new Label
            { Text = "Bibliothèque", Left = 16, Top = y + 4, Width = 100, ForeColor = Theme.Discret });
            cbProfils = new ComboBox
            {
                Left = 16, Top = y + 24, Width = 318, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.Champ, ForeColor = Theme.Texte, FlatStyle = FlatStyle.Flat
            };
            gauche.Controls.Add(cbProfils);
            y += 56;
            var bEnrProfil = Bouton("Enregistrer", 104, EnregistrerProfil);
            bEnrProfil.Left = 16; bEnrProfil.Top = y; gauche.Controls.Add(bEnrProfil);
            var bChgProfil = Bouton("Charger", 104, ChargerProfil);
            bChgProfil.Left = 122; bChgProfil.Top = y; gauche.Controls.Add(bChgProfil);
            var bSupProfil = Bouton("Supprimer", 104, SupprimerProfil);
            bSupProfil.Left = 228; bSupProfil.Top = y; gauche.Controls.Add(bSupProfil);
            y += 38;
            RafraichirProfils();

            // --- EN BAS : réglages machine détaillés, repliés par défaut ---
            gauche.Controls.Add(new Panel
            { Left = 16, Top = y + 6, Width = LargeurPanneau, Height = 1, BackColor = Theme.Separateur });
            y += 12;
            btnMachHead = new Button
            {
                Text = "▸  RÉGLAGES MACHINE (cotes)", Left = 16, Top = y, Width = 316, Height = 26,
                FlatStyle = FlatStyle.Flat, BackColor = Theme.Panneau, ForeColor = Theme.Accent,
                TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btnMachHead.FlatAppearance.BorderSize = 0;
            btnMachHead.Click += (s, e) => BasculerMachPanel();
            gauche.Controls.Add(btnMachHead);
            y += 30;

            machPanel = new Panel { Left = 0, Top = y, Width = 352, Visible = false, BackColor = Theme.Panneau };
            gauche.Controls.Add(machPanel);

            int my = 0;
            nHauteurPoincon = Num(machPanel, "Hauteur poinçon", poincon.Hauteur, 60, 250, 5, 0, ref my, v =>
            {
                poincon.Hauteur = v;
                vueSection.Outillage(plieuse, poincon, matrice, atelier.Embase);
                vue3D.Outillage(plieuse, poincon, matrice, atelier.Embase);
                Recalculer();
            });

            my = TitreVerrou(machPanel, "MACHINE — cotes", ref my);
            // Chaque champ lit ET écrit la plieuse COURANTE (le champ « plieuse », pas une
            // copie prise à la construction) : au changement de machine on les recharge.
            NumMachine(machPanel, "Butée mini", () => plieuse.ButeeMin, ref my, v => plieuse.ButeeMin = v);
            NumMachine(machPanel, "Butée maxi", () => plieuse.ButeeMax, ref my, v => plieuse.ButeeMax = v);
            NumMachine(machPanel, "Garde tablier", () => plieuse.TablierHauteur, ref my, v => plieuse.TablierHauteur = v);
            NumMachine(machPanel, "Hauteur libre", () => plieuse.HauteurLibre, ref my, v => plieuse.HauteurLibre = v);
            NumMachine(machPanel, "Tablier déport", () => plieuse.TablierDeport, ref my, v => plieuse.TablierDeport = v);
            NumMachine(machPanel, "Tonnage maxi (t)", () => plieuse.TonnageMax, ref my, v => plieuse.TonnageMax = v);
            NumMachine(machPanel, "Doigt : hauteur", () => plieuse.DoigtHauteur, ref my, v => plieuse.DoigtHauteur = v);
            NumMachine(machPanel, "Doigt : contact", () => plieuse.DoigtContact, ref my, v => plieuse.DoigtContact = v);

            my = Titre(machPanel, "EMBASES", my);
            NumMachine(machPanel, "Porte-poinçon H", () => atelier.Embase.PortePoinconH, ref my, v => atelier.Embase.PortePoinconH = v);
            NumMachine(machPanel, "Porte-poinçon L", () => atelier.Embase.PortePoinconLg, ref my, v => atelier.Embase.PortePoinconLg = v);
            NumMachine(machPanel, "Semelle H", () => atelier.Embase.SemelleH, ref my, v => atelier.Embase.SemelleH = v);
            NumMachine(machPanel, "Semelle L", () => atelier.Embase.SemelleLg, ref my, v => atelier.Embase.SemelleLg = v);
            machPanel.Height = my + 8;

            AppliquerVerrouMachine();   // état initial : verrouillé

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

            var barre = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Theme.Panneau };
            barre.Paint += (snd, pe) =>
            {
                using var pen = new Pen(Theme.Separateur, 1);
                pe.Graphics.DrawLine(pen, 0, barre.Height - 1, barre.Width, barre.Height - 1);
            };
            zoneDroite.Controls.Add(barre);

            // Trois zones dockees : elles ne peuvent plus se chevaucher quand on redimensionne.
            // Ordre d'ajout = ordre de reservation de l'espace : droite, gauche, puis le reste.

            var onglets = new FlowLayoutPanel
            {
                // Largeur = la somme des onglets, marges comprises. WrapContents = false :
                // un onglet qui ne rentre pas est dessine HORS du panneau, donc invisible —
                // il existe, il est cliquable nulle part. Ajouter un onglet = elargir ici.
                //   Pupitre 92+6 · Section 92+6 · Developpe 100+6 · 3D 60+6 = 368  (+8 padding)
                Dock = DockStyle.Right, Width = 380, BackColor = Theme.Panneau,
                Padding = new Padding(0, 9, 8, 0), Margin = new Padding(0),
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false
            };
            barre.Controls.Add(onglets);

            ongletPupitre   = Onglet("Pupitre", 92, () => Vue(0));
            ongletSection   = Onglet("Section", 92, () => Vue(1));
            ongletDeveloppe = Onglet("Développé", 100, () => Vue(2));
            onglet3D        = Onglet("3D", 60, () => Vue(3));
            onglets.Controls.Add(ongletPupitre);
            onglets.Controls.Add(ongletSection);
            onglets.Controls.Add(ongletDeveloppe);
            onglets.Controls.Add(onglet3D);

            var navig = new Panel { Dock = DockStyle.Left, Width = 330, BackColor = Theme.Panneau };
            barre.Controls.Add(navig);

            var bPrec = Bouton("◀", 44, () => AllerEtape(etape - 1));
            bPrec.Left = 10; bPrec.Top = 9; bPrec.Height = 34; navig.Controls.Add(bPrec);
            var bSuiv = Bouton("▶", 44, () => AllerEtape(etape + 1));
            bSuiv.Left = 58; bSuiv.Top = 9; bSuiv.Height = 34; navig.Controls.Add(bSuiv);

            tbEtape = new TrackBar
            {
                Left = 110, Top = 8, Width = 210, Minimum = 0, Maximum = 1,
                TickStyle = TickStyle.None, BackColor = Theme.Panneau
            };
            tbEtape.ValueChanged += (s, e) => { if (!_load) AllerEtape(tbEtape.Value); };
            navig.Controls.Add(tbEtape);

            lblEtape = new Label
            {
                Dock = DockStyle.Fill, ForeColor = Theme.Accent, AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 10, 0),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            barre.Controls.Add(lblEtape);
            lblEtape.BringToFront();

            vuePupitre = new VuePupitre { Dock = DockStyle.Fill };
            vueSection = new VueSection { Dock = DockStyle.Fill, Visible = false };
            vueDeveloppe = new VueDeveloppe { Dock = DockStyle.Fill, Visible = false };
            vue3D        = new VueVolume { Dock = DockStyle.Fill, Visible = false };
            zoneDroite.Controls.Add(vuePupitre);
            zoneDroite.Controls.Add(vueSection);
            zoneDroite.Controls.Add(vueDeveloppe);
            zoneDroite.Controls.Add(vue3D);
            vuePupitre.BringToFront();
            MajOnglets(0);
            vueSection.Outillage(plieuse, poincon, matrice, atelier.Embase);
                vue3D.Outillage(plieuse, poincon, matrice, atelier.Embase);

            // Une saisie FAITE DANS le pupitre : on rafraîchit tout le reste, mais on ne
            // reconstruit pas sa grille (elle est en pleine validation de cellule — voir
            // VuePupitre.MettreAJourValeurs).
            vuePupitre.Edited += () => { ChargerPans(); Recalculer(reconstruirePupitre: false); };
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
            vuePupitre.Visible = i == 0;
            vueSection.Visible = i == 1;
            vueDeveloppe.Visible = i == 2;
            vue3D.Visible = i == 3;
            if (i == 0) vuePupitre.BringToFront();
            else if (i == 1) vueSection.BringToFront();
            else if (i == 2) vueDeveloppe.BringToFront();
            else vue3D.BringToFront();
            MajOnglets(i);
        }

        /// <summary>Onglet actif : fond accentue, bord orange. Les autres restent neutres.</summary>
        void MajOnglets(int actif)
        {
            var l = new[] { ongletPupitre, ongletSection, ongletDeveloppe, onglet3D };
            for (int i = 0; i < l.Length; i++)
            {
                if (l[i] == null) continue;
                bool on = i == actif;
                l[i].BackColor = on ? Theme.Champ : Theme.Bouton;
                l[i].ForeColor = on ? Theme.Accent : Theme.Discret;
                l[i].FlatAppearance.BorderColor = on ? Theme.Accent : Theme.Bord;
                l[i].FlatAppearance.BorderSize = on ? 1 : 1;
            }
        }

        Button Onglet(string t, int w, Action onClick)
        {
            var b = new Button
            {
                Text = t, Width = w, Height = 34, FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Bouton, ForeColor = Theme.Discret,
                Margin = new Padding(3, 0, 3, 0),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            b.FlatAppearance.BorderColor = Theme.Bord;
            b.Click += (s, e) => onClick();
            return b;
        }

        // ------------------------------------------------- édition pièce --

        void DefinirNbPlis(int nb)
        {
            nb = Math.Max(1, nb);
            int pans = nb + 1;
            while (piece.Segments.Count < pans) piece.Segments.Add(100);
            while (piece.Segments.Count > pans) piece.Segments.RemoveAt(piece.Segments.Count - 1);
            piece.Sequence.RemoveAll(o => o.Bend >= piece.NbPlis);
            CompleterSequence();
            ChargerPans();
            Recalculer();
        }

        /// <summary>
        /// Une opération pour chaque pli qui n'en a pas. Le pupitre et la grille PLIS affichent
        /// une ligne par OPÉRATION : un pli sans opération n'apparaissait nulle part. Monter
        /// « Nombre de plis » de 1 à 3 ajoutait deux pans invisibles, impossibles à régler.
        /// </summary>
        void CompleterSequence()
        {
            var faits = new HashSet<int>();
            foreach (var o in piece.Sequence) if (o.Axe == 0) faits.Add(o.Bend);
            for (int b = 0; b < piece.NbPlis; b++)
                if (!faits.Contains(b))
                    piece.Sequence.Add(new Operation { Bend = b, AngleCible = 90, Sens = Sens.Haut, V = VParDefaut() });
        }

        // ------------------------------------------------- saisie des pans --

        static string PansEnTexte(Piece p)
        {
            var parts = new List<string>();
            foreach (double v in p.Segments) parts.Add(v.ToString("0.###", CultureInfo.InvariantCulture));
            return string.Join(" ", parts);
        }

        void AfficherPans()
        {
            if (txtPans == null) return;
            bool avant = _load;
            _load = true;
            txtPans.Text = PansEnTexte(piece);
            _load = avant;
        }

        /// <summary>« 30 40 150 200 100 10 » -> liste de pans. null si illisible.</summary>
        static List<double> LirePansTexte(string texte)
        {
            var res = new List<double>();
            var mots = (texte ?? "").Split(new[] { ' ', '\t', ';', '·', '/', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string m in mots)
            {
                if (!double.TryParse(m.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return null;
                if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0) return null;
                res.Add(v);
            }
            return res;
        }

        /// <summary>
        /// Applique les pans tapés. Même nombre de pans : seules les longueurs changent, la
        /// gamme est conservée. Nombre différent : on ajuste les plis comme « Nombre de plis ».
        /// </summary>
        void ValiderPans(bool bavard)
        {
            if (_load || txtPans == null) return;
            // Texte inchangé = rien à faire. Indispensable : sortir du champ sans rien taper ne
            // doit pas réécrire les pans avec leur valeur ARRONDIE à l'affichage.
            if (txtPans.Text.Trim() == PansEnTexte(piece)) return;
            var vals = LirePansTexte(txtPans.Text);
            int maxPans = (int)nNbPlis.Maximum + 1;
            if (vals == null || vals.Count < 2 || vals.Count > maxPans)
            {
                if (bavard)
                    MessageBox.Show("Pans illisibles.\n\nTape les longueurs dans l'ordre du profil, séparées par un espace"
                        + $" (de 2 à {maxPans} pans, toutes > 0) :\n30 40 150 200 100 10",
                        "Pans", MessageBoxButtons.OK, MessageBoxIcon.Information);
                AfficherPans();
                return;
            }

            bool identique = vals.Count == piece.Segments.Count;
            for (int i = 0; identique && i < vals.Count; i++)
                if (Math.Abs(vals[i] - piece.Segments[i]) > 1e-9) identique = false;
            if (identique) { AfficherPans(); return; }

            piece.Segments.Clear();
            piece.Segments.AddRange(vals);
            piece.Sequence.RemoveAll(o => o.Bend >= piece.NbPlis);
            CompleterSequence();
            ChargerPans();
            Recalculer();
        }

        int IndexMatiere(double rm)
        {
            int best = 0;
            for (int i = 1; i < MatiereRm.Length; i++)
                if (Math.Abs(MatiereRm[i] - rm) < Math.Abs(MatiereRm[best] - rm)) best = i;
            return best;
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

        /// <summary>
        /// Cherche un ordre de pliage sans collision. Pour chaque pli on essaie les quatre
        /// engagements, du moins manipulé au plus manipulé :
        ///   direct  ·  rotation 180° à plat (⇄)  ·  retourné dessus/dessous (⇅)  ·  les deux.
        /// </summary>
        void OrdreAuto()
        {
            if (EssayerOrdreAuto(out int plat, out int face))
            {
                string m = "Ordre sans collision trouvé.";
                if (plat > 0) m += $"\n{plat} retournement(s) à plat (⇄) — confort opérateur.";
                if (face > 0) m += $"\n{face} retournement(s) dessus/dessous (⇅).";
                if (plat == 0 && face == 0) m += "\nAucune manipulation de la pièce.";
                MessageBox.Show(m, "Ordre auto", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Aucun ordre sans collision trouvé, même en retournant la pièce.",
                    "Ordre auto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Résout la séquence sans UI : applique l'ordre trouvé, ou restaure l'existant.
        /// Utilisé au démarrage (silencieux) et par le bouton Ordre auto.
        /// </summary>
        bool EssayerOrdreAuto(out int plat, out int face)
        {
            plat = 0; face = 0;
            var initial = new List<Operation>(piece.Sequence);

            // On appelle le VRAI solveur (Pliage/Solveur.cs) : contrainte « pli fermé en
            // premier », classement par prise opérateur, collision tablier, plancher d'angle,
            // parité de face. C'est lui la référence, testée au banc — pas une exploration
            // maison. On lui passe la pièce telle qu'affichée : longueurs, angles, faces, V.
            piece.AssurerForme();
            int n = piece.NbPlis;
            if (n == 0 || piece.Segments.Count < 2) { Recalculer(); return false; }

            var segments = new List<double>(piece.Segments);
            var faces = new int[n];
            var angles = new double[n];
            var vs = new double[n];
            // Les faces viennent de piece.Faces : saisies dans la colonne Face (FacesManuelles),
            // ou sinon déduites par AssurerForme du drapeau ⇅ de la séquence. Pour une pièce
            // saisie de zéro, il FAUT renseigner les faces avant l'ordre auto : sans ça tout est
            // « même face » et le solveur peut résoudre une pièce impossible (cf. Z spirale).
            for (int b = 0; b < n; b++)
            {
                angles[b] = piece.Angles[b];
                faces[b] = piece.Faces[b] ? 1 : 0;
                // V de chaque pli : celui déjà choisi dans la séquence pour ce pli, sinon défaut.
                double v = VParDefaut();
                foreach (var o in initial) if (o.Bend == b) { v = o.V; break; }
                vs[b] = v;
            }

            // On résout DANS LE MODE DE LA PIÈCE (faces saisies ou non) : le solveur teste alors
            // chaque candidat exactement comme le moteur le dessinera une fois la gamme appliquée.
            var sols = Solveur.Resoudre(segments, faces, angles, vs, plieuse, poincon, matrice,
                                        atelier.Embase, piece.Epaisseur, plieuse.ButeeMin,
                                        facesManuelles: piece.FacesManuelles);

            if (sols.Count == 0)
            {
                piece.Sequence = initial;   // rien trouvé : on ne casse pas ce qui était là
                Recalculer();
                return false;
            }

            // meilleure solution = la 1re (le solveur trie déjà : fermé-d'abord, prise, manip)
            piece.Sequence = new List<Operation>(sols[0].Sequence);
            etape = 0;
            // On compte les GESTES entre deux étapes — ce que la vue section affiche — pas les
            // cases cochées : un ⇅ qui reste coché deux étapes de suite n'est qu'UN geste, et
            // quand la face change c'est ⇅ tout seul (jamais ⇅ + ⇄ pour la même main).
            for (int i = 1; i < piece.Sequence.Count; i++)
            {
                var a = piece.Sequence[i - 1]; var b = piece.Sequence[i];
                if (a.Retournee != b.Retournee) face++;
                else if (a.ButeeAval != b.ButeeAval) plat++;
            }
            Recalculer();
            return true;
        }

        // NB : l'exploration d'ordre est faite par Pliage/Solveur.cs (le vrai solveur, testé
        // au banc). L'ancienne recherche maison « Explorer » a été retirée — elle doublonnait
        // le solveur sans connaître la règle « fermé d'abord » ni le classement par prise.

        double VParDefaut()
        {
            if (piece.Sequence.Count > 0) return piece.Sequence[^1].V;
            return matrice != null && matrice.Vs.Count > 0 ? matrice.Vs[0].V : 16;
        }

        // -------------------------------------------------- fichier pièce --

        void NouvellePiece()
        {
            piece = Piece.DemoZLaque();      // chevêtre = Piece.Demo()
            _fichier = null;
            etape = 0;
            AppliquerPiece(resoudre: false);   // on garde la séquence figée de la démo
        }

        void OuvrirPiece()
        {
            using var d = new OpenFileDialog { Filter = PieceIO.Filtre, Title = "Ouvrir une pièce" };
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                piece = PieceIO.Charger(d.FileName);
                _fichier = d.FileName;
                etape = 0;
                AppliquerPiece();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lecture impossible :\n" + ex.Message,
                    "Ouvrir", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void EnregistrerPiece()
        {
            if (string.IsNullOrEmpty(_fichier)) { EnregistrerPieceSous(); return; }
            Sauver(_fichier);
        }

        void EnregistrerPieceSous()
        {
            using var d = new SaveFileDialog
            {
                Filter = PieceIO.Filtre, Title = "Enregistrer la pièce",
                DefaultExt = PieceIO.Extension, AddExtension = true,
                FileName = "piece." + PieceIO.Extension
            };
            if (d.ShowDialog(this) != DialogResult.OK) return;
            _fichier = d.FileName;
            Sauver(_fichier);
        }

        void Sauver(string chemin)
        {
            try
            {
                // (LirePans() a été retiré : la grille PLIS affiche des COTES DE BUTÉE par étape,
                //  pas les pans. Les relire dans Segments abîmait la pièce à chaque Enregistrer —
                //  le chéneau 30·40·150·200·100·10 était écrit 10·100·30·40·200·10.)
                piece.NormaliserReprises();
                PieceIO.Sauver(piece, chemin);
                MajTitre();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Écriture impossible :\n" + ex.Message,
                    "Enregistrer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Remet toute l'UI en phase avec la pièce courante (neuve ou chargée).</summary>
        void AppliquerPiece(bool resoudre = false)
        {
            piece.NormaliserReprises();
            _load = true;
            if (nNbPlis != null)
                nNbPlis.Value = Math.Min(nNbPlis.Maximum, Math.Max(nNbPlis.Minimum, (decimal)piece.NbPlis));
            if (nEpaisseur != null)
                nEpaisseur.Value = (decimal)Math.Max((double)nEpaisseur.Minimum,
                                    Math.Min((double)nEpaisseur.Maximum, piece.Epaisseur));
            if (cbCotes != null) cbCotes.SelectedIndex = piece.CotesExterieures ? 1 : 0;
            if (nLongPli != null)
                nLongPli.Value = (decimal)Math.Max((double)nLongPli.Minimum,
                                  Math.Min((double)nLongPli.Maximum, piece.LongueurPli));
            if (cbMatiere != null) cbMatiere.SelectedIndex = IndexMatiere(piece.Rm);
            if (txtNom != null) txtNom.Text = piece.Nom ?? "";
            if (txtChantier != null) txtChantier.Text = piece.Chantier ?? "";
            _load = false;
            ChargerPans();
            if (resoudre) EssayerOrdreAuto(out _, out _); else Recalculer();
            MajTitre();
        }

        void MajTitre() =>
            Text = string.IsNullOrEmpty(_fichier)
                ? "Simulateur de pliage — collisions outillage · TolTem"
                : $"Simulateur de pliage — {System.IO.Path.GetFileName(_fichier)} · TolTem";

        // ---------------------------------------------------- bibliothèque --

        void RafraichirProfils()
        {
            if (cbProfils == null) return;
            _profils.Clear();
            _profils.AddRange(biblio.Profils);
            cbProfils.Items.Clear();
            foreach (var pr in _profils) cbProfils.Items.Add(pr.Libelle);
            if (cbProfils.Items.Count > 0) cbProfils.SelectedIndex = 0;
        }

        void EnregistrerProfil()
        {
            piece.Nom = (txtNom?.Text ?? "").Trim();
            piece.Chantier = (txtChantier?.Text ?? "").Trim();
            if (piece.Nom.Length == 0)
            {
                MessageBox.Show("Donne un nom au profil avant de l'enregistrer.",
                    "Profils", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtNom?.Focus();
                return;
            }
            // Le chantier « Références » porte les pièces étalons, figées dans l'exe : on n'y
            // enregistre rien (elles sont remises à l'identique à chaque démarrage).
            if (Bibliotheque.EstChantierReference(piece.Chantier))
            {
                MessageBox.Show("Le chantier « Références » est réservé aux pièces étalons.\n"
                    + "Donne un autre nom de chantier (ou laisse-le vide) pour enregistrer ta pièce.",
                    "Profils", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtChantier?.Focus();
                return;
            }
            biblio.Enregistrer(piece, piece.Nom, piece.Chantier);
            RafraichirProfils();
            int i = _profils.FindIndex(x =>
                string.Equals(x.Nom, piece.Nom, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Chantier ?? "", piece.Chantier, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) cbProfils.SelectedIndex = i;
        }

        void ChargerProfil()
        {
            int i = cbProfils?.SelectedIndex ?? -1;
            if (i < 0 || i >= _profils.Count)
            {
                MessageBox.Show("Choisis un profil dans la bibliothèque.",
                    "Profils", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var p = biblio.Instancier(_profils[i]);
            if (p == null) return;
            // Une référence chargée devient une pièce de travail ordinaire : on vide son
            // chantier, pour qu'un Enregistrer crée TA copie au lieu de viser l'étalon.
            if (Bibliotheque.EstReference(_profils[i])) p.Chantier = "";
            piece = p;
            _fichier = null;
            etape = 0;
            AppliquerPiece();   // garde l'ordre enregistré du profil
        }

        void SupprimerProfil()
        {
            int i = cbProfils?.SelectedIndex ?? -1;
            if (i < 0 || i >= _profils.Count) return;
            var pr = _profils[i];
            if (Bibliotheque.EstReference(pr))
            {
                MessageBox.Show("Les pièces de « Références » sont les étalons de l'atelier : elles ne se suppriment pas.",
                    "Profils", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show($"Supprimer le profil « {pr.Libelle} » ?", "Profils",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            biblio.Supprimer(pr);
            RafraichirProfils();
        }

        /// <summary>
        /// Bascule la face d'un pli FNL <-> FL depuis la grille PANS. La face est une donnée
        /// de la PIÈCE (elle vient du dessin / de la tôle), indépendante de la séquence :
        /// FL = côté brillant / laqué / galva-zinc traité (vers le visible), FNL = l'autre côté.
        /// On écrit dans piece.Faces, puis on ré-affiche. On NE touche pas à la séquence : c'est
        /// au solveur (bouton Ordre auto) de déduire les retournements à partir des faces.
        /// </summary>
        void BasculerFace(int ligne, int colonne)
        {
            if (_load || ligne < 0 || colonne < 0) return;
            if (dgPans.Columns[colonne].Name != "face") return;   // seule la colonne Face réagit
            piece.AssurerForme();
            if (ligne >= piece.Sequence.Count) return;            // la ligne = une étape de pliage
            int b = piece.Sequence[ligne].Bend;                   // le pli (bend) de cette étape
            if (b < 0 || b >= piece.Faces.Count) return;

            // Première face saisie : on passe la pièce en « faces saisies » SANS rien bouger
            // (Piece.FigerFaces recale les ⇄). Sinon ce clic changeait le côté de butée de tous
            // les autres plis d'un coup.
            piece.FigerFaces();
            piece.Faces[b] = !piece.Faces[b];                     // FNL <-> FL sur ce pli, et lui seul

            ChargerPans();
            Recalculer();
        }

        void ChargerPans()
        {
            _load = true;
            piece.AssurerForme();
            dgPans.Rows.Clear();
            // Une ligne = UN PLI (dans l'ordre de la séquence de pliage). La longueur affichée
            // est la cote de butée du pli (le pan calé contre les doigts) — Piece.CoteButee, la
            // même règle que le moteur et le pupitre. Affichage seul : on ne modifie pas la pièce.
            for (int e = 0; e < piece.Sequence.Count; e++)
            {
                var op = piece.Sequence[e];
                int b = op.Bend;
                bool okFace = b >= 0 && b < piece.Faces.Count;
                dgPans.Rows.Add(
                    (e + 1).ToString(),
                    piece.CoteButee(op).ToString("0.#", CultureInfo.InvariantCulture),
                    op.AngleCible.ToString("0.#", CultureInfo.InvariantCulture) + "\u00B0",
                    okFace ? (piece.Faces[b] ? "FL" : "FNL") : "—");
            }
            if (nNbPlis != null)
                nNbPlis.Value = Math.Min(nNbPlis.Maximum, Math.Max(nNbPlis.Minimum, (decimal)piece.NbPlis));
            AfficherPans();
            _load = false;
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
            SurlignerPansDuPli(etape);
        }

        // Surligne dans la grille PLIS la ligne de l'étape affichée. (Une ligne = une étape
        // depuis que la grille liste les plis : l'ancien code surlignait les lignes « pan b »
        // et « pan b+1 », donc pas la bonne.)
        void SurlignerPansDuPli(int etapeIdx)
        {
            _ligneSurl = (etapeIdx >= 0 && etapeIdx < piece.Sequence.Count) ? etapeIdx : -1;
            dgPans?.Invalidate();
        }

        /// <param name="reconstruirePupitre">false quand la saisie vient du pupitre lui-même :
        /// sa grille est mise à jour en place, jamais reconstruite pendant qu'elle valide une
        /// cellule (sinon WinForms lève « reentrant call to SetCurrentCellAddressCore »).</param>
        void Recalculer(bool reconstruirePupitre = true)
        {
            piece.NormaliserReprises();
            // La grille PLIS liste les étapes DANS L'ORDRE DE LA GAMME : elle doit suivre tout ce
            // qui change la séquence. Avant, Ordre auto, ↑ / ↓ et Trier ne la rechargeaient pas —
            // elle restait sur l'ancien ordre pendant que le pupitre et l'écran montraient le
            // nouveau. On la recharge ici, une fois pour toutes.
            ChargerPans();
            _load = true;
            tbEtape.Maximum = Math.Max(0, piece.Sequence.Count - 1);
            etape = Math.Max(0, Math.Min(tbEtape.Maximum, etape));
            tbEtape.Value = Math.Min(tbEtape.Maximum, Math.Max(tbEtape.Minimum, etape));
            _load = false;

            ListerSequence();
            Redessiner();
            if (reconstruirePupitre)
                vuePupitre.Afficher(piece, etape, plieuse, poincon, matrice, atelier.Embase);
            else
                vuePupitre.MettreAJourValeurs(etape);
            SurlignerPansDuPli(etape);
        }

        // ------------------------------------------------------ autotest --

        /// <summary>
        /// Lance le banc de contrôle interne (Pliage/Autotest.cs) et affiche son rapport.
        /// Toujours sur l'outillage de référence — Loire Safe, Rolleri, matrice 2045 — celui
        /// sur lequel les pièces étalons ont été validées, quel que soit le choix à l'écran.
        /// </summary>
        void LancerAutotest()
        {
            var mat = atelier.Matrices.Find(m => m.Nom.Contains("2045")) ?? atelier.Matrices[0];
            string rapport;
            try { rapport = Autotest.Executer(atelier.Plieuses[0], atelier.Poincons[0], mat, atelier.Embase); }
            catch (Exception ex) { rapport = "ECHEC — l'autotest a planté :\r\n\r\n" + ex; }
            bool ok = rapport.StartsWith("OK");

            using var f = new Form
            {
                Text = ok ? "Autotest — les règles tiennent" : "Autotest — ÉCHEC",
                Width = 760, Height = 680, StartPosition = FormStartPosition.CenterParent,
                BackColor = Theme.Fond, ForeColor = Theme.Texte, Font = Font,
                ShowInTaskbar = false, MinimizeBox = false, ShowIcon = false
            };
            var t = new TextBox
            {
                Multiline = true, ReadOnly = true, WordWrap = false, Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both, BorderStyle = BorderStyle.None,
                BackColor = Theme.Champ, ForeColor = ok ? Theme.Texte : Theme.Alerte,
                Font = new Font("Consolas", 9.5f),
                Text = rapport.Replace("\r\n", "\n").Replace("\n", "\r\n")
            };
            f.Controls.Add(t);
            f.Shown += (snd, ev) => t.Select(0, 0);
            f.ShowDialog(this);
        }

        void Redessiner()
        {
            var etat = Moteur.Construire(piece, etape, plieuse, poincon, matrice, atelier.Embase);
            vueSection.Afficher(etat, piece);
            vue3D.Afficher(etat, piece);
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
                    Left = 16, Top = y + 6, Width = LargeurPanneau, Height = 1, BackColor = Theme.Separateur
                });
                y += 12;
            }
            p.Controls.Add(new Label
            {
                Text = t, Left = 16, Top = y + 6, Width = LargeurPanneau,
                ForeColor = Theme.Accent, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            });
            return y + 30;
        }

        /// <summary>Titre de section avec cadenas (🔒/🔓), bouton Valider et indicateur « modifié ».</summary>
        int TitreVerrou(Panel p, string t, ref int y)
        {
            p.Controls.Add(new Panel
            {
                Left = 16, Top = y + 6, Width = LargeurPanneau, Height = 1, BackColor = Theme.Separateur
            });
            y += 12;
            p.Controls.Add(new Label
            {
                Text = t, Left = 16, Top = y + 6, Width = 170,
                ForeColor = Theme.Accent, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            });

            lblMachModifie = new Label
            {
                Left = 176, Top = y + 8, Width = 64, ForeColor = Theme.Accent,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold), Text = ""
            };
            p.Controls.Add(lblMachModifie);

            btnValiderMachine = new Button
            {
                Text = "Valider", Left = 242, Top = y + 2, Width = 60, Height = 24,
                FlatStyle = FlatStyle.Flat, BackColor = Theme.Bouton, ForeColor = Theme.Texte,
                Font = new Font("Segoe UI", 8.5f), Visible = false
            };
            btnValiderMachine.FlatAppearance.BorderColor = Theme.Bord;
            btnValiderMachine.Click += (s, e) => ValiderMachine();
            p.Controls.Add(btnValiderMachine);

            btnVerrou = new Button
            {
                Text = "🔒", Left = 306, Top = y + 2, Width = 30, Height = 24,
                FlatStyle = FlatStyle.Flat, BackColor = Theme.Bouton, ForeColor = Theme.Texte,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnVerrou.FlatAppearance.BorderColor = Theme.Bord;
            btnVerrou.Click += (s, e) => BasculerVerrouMachine();
            var tip = new ToolTip();
            tip.SetToolTip(btnVerrou, "Verrouiller / déverrouiller les réglages machine");
            p.Controls.Add(btnVerrou);

            return y + 30;
        }

        void BasculerMachPanel()
        {
            if (machPanel == null) return;
            machPanel.Visible = !machPanel.Visible;
            btnMachHead.Text = (machPanel.Visible ? "▾  " : "▸  ") + "RÉGLAGES MACHINE (cotes)";
        }

        void BasculerVerrouMachine()
        {
            // Reverrouiller avec des modifs non validées : on redemande.
            if (!_machVerrouille && _machModifie)
            {
                var r = MessageBox.Show(
                    "Des réglages machine ont été modifiés sans être validés.\nValider avant de verrouiller ?",
                    "Réglages machine", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return;
                if (r == DialogResult.Yes) { ValiderMachine(); return; }
                _machModifie = false;   // Non : on verrouille, changements gardés en session mais non enregistrés
            }
            _machVerrouille = !_machVerrouille;
            AppliquerVerrouMachine();
        }

        void ValiderMachine()
        {
            atelier.Sauver();          // persiste les cotes dans atelier.json
            _machModifie = false;
            _machVerrouille = true;
            AppliquerVerrouMachine();
        }

        /// <summary>Applique l'état du verrou aux champs et met à jour cadenas / Valider / « modifié ».</summary>
        void AppliquerVerrouMachine()
        {
            foreach (var n in _champsMachine)
            {
                n.Enabled = !_machVerrouille;
                n.BackColor = _machVerrouille ? Theme.Panneau : Theme.Champ;
            }
            MajEtatVerrou();
        }

        void MajEtatVerrou()
        {
            if (btnVerrou != null) btnVerrou.Text = _machVerrouille ? "🔒" : "🔓";
            if (btnValiderMachine != null) btnValiderMachine.Visible = !_machVerrouille;
            if (lblMachModifie != null) lblMachModifie.Text = _machModifie ? "● modifié" : "";
        }

        TextBox Texte(Panel p, string lab, string v, ref int y, Action<string> onChange)
        {
            p.Controls.Add(new Label { Text = lab, Left = 16, Top = y + 4, Width = 80, ForeColor = Theme.Texte });
            var t = new TextBox
            {
                Left = 100, Top = y, Width = 238, Text = v ?? "",
                BackColor = Theme.Champ, ForeColor = Theme.Texte, BorderStyle = BorderStyle.FixedSingle
            };
            t.TextChanged += (s, e) => { if (!_load) onChange(t.Text); };
            p.Controls.Add(t);
            y += 30;
            return t;
        }

        NumericUpDown Num(Panel p, string lab, double v, double min, double max,
                          double inc, int dec, ref int y, Action<double> onChange)
        {
            p.Controls.Add(new Label { Text = lab, Left = 16, Top = y + 4, Width = 170, ForeColor = Theme.Texte });
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

        void NumMachine(Panel p, string lab, Func<double> lire, ref int y, Action<double> onChange)
        {
            p.Controls.Add(new Label { Text = lab, Left = 24, Top = y + 4, Width = 164, ForeColor = Theme.Discret });
            var n = new NumericUpDown
            {
                Left = 190, Top = y, Width = 148, Minimum = 0, Maximum = 5000,
                DecimalPlaces = 1, Increment = 1, Value = BornerMachine(lire()),
                BackColor = Theme.Champ, ForeColor = Theme.Texte, BorderStyle = BorderStyle.FixedSingle
            };
            n.ValueChanged += (s, e) =>
            {
                if (_load) return;
                onChange((double)n.Value);   // aperçu live
                _machModifie = true;
                MajEtatVerrou();
                Recalculer();
            };
            _champsMachine.Add(n);
            _lecturesMachine.Add(lire);
            p.Controls.Add(n);
            y += 28;
        }

        // une cote hors 0–5000 dans un atelier.json retouché ne doit pas planter le champ
        static decimal BornerMachine(double v)
            => (decimal)Math.Max(0, Math.Min(5000, double.IsNaN(v) ? 0 : v));

        /// <summary>Remet dans les champs les cotes de la plieuse courante (changement de machine).</summary>
        void RechargerChampsMachine()
        {
            bool avant = _load;
            _load = true;
            for (int i = 0; i < _champsMachine.Count && i < _lecturesMachine.Count; i++)
                _champsMachine[i].Value = BornerMachine(_lecturesMachine[i]());
            _load = avant;
        }

        ComboBox Combo(Panel p, string lab, string[] items, int sel, ref int y, Action<int> onChange)
        {
            p.Controls.Add(new Label { Text = lab, Left = 16, Top = y + 4, Width = 170, ForeColor = Theme.Texte });
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
                Left = 16, Top = y, Width = LargeurPanneau, Height = h,
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
