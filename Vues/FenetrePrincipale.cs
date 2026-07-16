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
        Button btnVerrou, btnValiderMachine;
        Label lblMachModifie;
        Panel machPanel;          // bloc machine repliable (en bas)
        Button btnMachHead;       // en-tête ▸/▾ du bloc machine

        ComboBox cbMachine, cbPoincon, cbMatrice, cbCotes, cbProfils;
        TextBox txtNom, txtChantier;
        readonly System.Collections.Generic.List<Profil> _profils = new();
        NumericUpDown nNbPlis, nEpaisseur, nHauteurPoincon;
        DataGridView dgPans;
        readonly System.Collections.Generic.HashSet<int> _pansSurl = new();  // pans bordant le pli sélectionné
        VueSection vueSection;
        VueDeveloppe vueDeveloppe;
        VuePupitre vuePupitre;
        TrackBar tbEtape;
        Button ongletPupitre, ongletSection, ongletDeveloppe;
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
            cbMatrice = Combo(gauche, "Matrice", Noms(atelier.Matrices),
                Math.Max(0, atelier.Matrices.IndexOf(matrice)), ref y, i =>
            {
                matrice = atelier.Matrices[i];
                vueSection.Outillage(plieuse, poincon, matrice, atelier.Embase);
                Recalculer();
            });

            // --- PIÈCE + PANS ---
            y = Titre(gauche, "PIÈCE", y);
            nNbPlis = Num(gauche, "Nombre de plis", piece.NbPlis, 1, 12, 1, 0, ref y, v => DefinirNbPlis((int)v));
            nEpaisseur = Num(gauche, "Épaisseur (mm)", piece.Epaisseur, 0.4, 5, 0.1, 2, ref y,
                v => { piece.Epaisseur = v; Recalculer(); });
            cbCotes = Combo(gauche, "Cotes", new[] { "intérieures", "extérieures" }, 0, ref y,
                i => { piece.CotesExterieures = i == 1; Recalculer(); });

            y = Titre(gauche, "PANS (longueurs mm)", y);
            // RAPPEL, PAS SAISIE. Tout se tape au pupitre — c'est lui la CN. Cette grille
            // n'est là que pour avoir la longueur, l'angle et la face CÔTE À CÔTE sous les yeux :
            // personne ne pourra dire « j'avais pas vu ». Elle se met à jour toute seule à chaque
            // édition du pupitre et après un ordre auto (vuePupitre.Edited -> ChargerPans).
            // Tout est en lecture seule : deux endroits pour saisir la même cote, c'est deux
            // endroits pour se tromper.
            dgPans = Grille(gauche, 150, ref y);
            dgPans.Columns.Add(Col("pan", "Pan", 40, true));
            dgPans.Columns.Add(Col("lg", "Longueur", 74, true));
            dgPans.Columns.Add(Col("ang", "Angle", 52, true));
            dgPans.Columns.Add(Col("face", "Face", 46, true));
            dgPans.ReadOnly = true;

            // --- SYNCHRO PANS ↔ PUPITRE ---
            // coloriage : les pans qui bordent le pli sélectionné dans le pupitre
            dgPans.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                bool sur = _pansSurl.Contains(e.RowIndex);
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
                if (p < 0) { vuePupitre.SurlignerPlis(); return; }
                vuePupitre.SurlignerPlis(p - 1, p);
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
            var bEnregSous = Bouton("Enregistrer sous…", 318, EnregistrerPieceSous);
            bEnregSous.Left = 16; bEnregSous.Top = y; gauche.Controls.Add(bEnregSous);
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
                Recalculer();
            });

            my = TitreVerrou(machPanel, "MACHINE — cotes", ref my);
            NumMachine(machPanel, "Butée mini", plieuse.ButeeMin, ref my, v => plieuse.ButeeMin = v);
            NumMachine(machPanel, "Butée maxi", plieuse.ButeeMax, ref my, v => plieuse.ButeeMax = v);
            NumMachine(machPanel, "Hauteur libre", plieuse.HauteurLibre, ref my, v => plieuse.HauteurLibre = v);
            NumMachine(machPanel, "Tablier déport", plieuse.TablierDeport, ref my, v => plieuse.TablierDeport = v);
            NumMachine(machPanel, "Tonnage maxi (t)", plieuse.TonnageMax, ref my, v => plieuse.TonnageMax = v);
            NumMachine(machPanel, "Doigt : hauteur", plieuse.DoigtHauteur, ref my, v => plieuse.DoigtHauteur = v);
            NumMachine(machPanel, "Doigt : contact", plieuse.DoigtContact, ref my, v => plieuse.DoigtContact = v);

            my = Titre(machPanel, "EMBASES", my);
            NumMachine(machPanel, "Porte-poinçon H", atelier.Embase.PortePoinconH, ref my, v => atelier.Embase.PortePoinconH = v);
            NumMachine(machPanel, "Porte-poinçon L", atelier.Embase.PortePoinconLg, ref my, v => atelier.Embase.PortePoinconLg = v);
            NumMachine(machPanel, "Semelle H", atelier.Embase.SemelleH, ref my, v => atelier.Embase.SemelleH = v);
            NumMachine(machPanel, "Semelle L", atelier.Embase.SemelleLg, ref my, v => atelier.Embase.SemelleLg = v);
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
                Dock = DockStyle.Right, Width = 306, BackColor = Theme.Panneau,
                Padding = new Padding(0, 9, 8, 0), Margin = new Padding(0),
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false
            };
            barre.Controls.Add(onglets);

            ongletPupitre   = Onglet("Pupitre", 92, () => Vue(0));
            ongletSection   = Onglet("Section", 92, () => Vue(1));
            ongletDeveloppe = Onglet("Développé", 100, () => Vue(2));
            onglets.Controls.Add(ongletPupitre);
            onglets.Controls.Add(ongletSection);
            onglets.Controls.Add(ongletDeveloppe);

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
            zoneDroite.Controls.Add(vuePupitre);
            zoneDroite.Controls.Add(vueSection);
            zoneDroite.Controls.Add(vueDeveloppe);
            vuePupitre.BringToFront();
            MajOnglets(0);
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
            vuePupitre.Visible = i == 0;
            vueSection.Visible = i == 1;
            vueDeveloppe.Visible = i == 2;
            if (i == 0) vuePupitre.BringToFront();
            else if (i == 1) vueSection.BringToFront();
            else vueDeveloppe.BringToFront();
            MajOnglets(i);
        }

        /// <summary>Onglet actif : fond accentue, bord orange. Les autres restent neutres.</summary>
        void MajOnglets(int actif)
        {
            var l = new[] { ongletPupitre, ongletSection, ongletDeveloppe };
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
            var ordre = new List<Operation>();
            var restant = new List<Operation>(initial);

            if (Explorer(ordre, restant))
            {
                piece.Sequence = ordre;
                etape = 0;
                foreach (var o in piece.Sequence)
                {
                    if (o.ButeeAval) plat++;
                    if (o.Retournee) face++;
                }
                Recalculer();
                return true;
            }

            piece.Sequence = initial;
            Recalculer();
            return false;
        }

        // Confort opérateur : on RETOURNE À PLAT (⇄) dès que c'est possible. Une tôle
        // qu'on tourne à plat sur la table se manipule sans forcer, alors que tenir un
        // grand pan en l'air casse le dos (intérimaires compris). Donc ButeeAval en
        // premier, direct seulement si le retournement ne pose pas à plat. ⇅ en dernier.
        static readonly (bool aval, bool face)[] Engagements =
            { (true, false), (false, false), (true, true), (false, true) };

        bool Explorer(List<Operation> ordre, List<Operation> restant)
        {
            if (restant.Count == 0) return true;

            for (int i = 0; i < restant.Count; i++)
            {
                var op = restant[i];
                restant.RemoveAt(i);

                bool memoA = op.ButeeAval, memoF = op.Retournee;
                foreach (var (aval, face) in Engagements)
                {
                    op.ButeeAval = aval;
                    op.Retournee = face;
                    ordre.Add(op);

                    var sauve = piece.Sequence;
                    piece.Sequence = new List<Operation>(ordre);
                    bool ok = !Moteur.Construire(piece, ordre.Count - 1, plieuse, poincon, matrice, atelier.Embase).Bloque;
                    piece.Sequence = sauve;

                    if (ok && Explorer(ordre, restant)) return true;
                    ordre.RemoveAt(ordre.Count - 1);
                }
                op.ButeeAval = memoA; op.Retournee = memoF;

                restant.Insert(i, op);
            }
            return false;
        }

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
                LirePans();
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
            LirePans();
            piece.Nom = (txtNom?.Text ?? "").Trim();
            piece.Chantier = (txtChantier?.Text ?? "").Trim();
            if (piece.Nom.Length == 0)
            {
                MessageBox.Show("Donne un nom au profil avant de l'enregistrer.",
                    "Profils", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtNom?.Focus();
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
            if (MessageBox.Show($"Supprimer le profil « {pr.Libelle} » ?", "Profils",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            biblio.Supprimer(pr);
            RafraichirProfils();
        }

        void ChargerPans()
        {
            _load = true;
            piece.AssurerForme();
            dgPans.Rows.Clear();
            for (int i = 0; i < piece.Segments.Count; i++)
            {
                // ligne du pan i : l'angle et la face décrivent le pli QUI SUIT ce pan.
                // Le dernier pan n'a pas de pli derrière lui.
                bool aPli = i < piece.NbPlis && i < piece.Angles.Count && i < piece.Faces.Count;
                dgPans.Rows.Add(
                    (i + 1).ToString(),
                    piece.Segments[i].ToString("0.#", CultureInfo.InvariantCulture),
                    aPli ? piece.Angles[i].ToString("0.#", CultureInfo.InvariantCulture) + "\u00B0" : "—",
                    aPli ? (piece.Faces[i] ? "FL" : "FNL") : "—");
            }
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
            SurlignerPansDuPli(etape);
        }

        // Surligne dans PANS les 2 pans qui bordent le pli de l'étape donnée
        // (pli b = entre pan b et pan b+1).
        void SurlignerPansDuPli(int etapeIdx)
        {
            _pansSurl.Clear();
            if (etapeIdx >= 0 && etapeIdx < piece.Sequence.Count)
            {
                int b = piece.Sequence[etapeIdx].Bend;
                _pansSurl.Add(b);
                _pansSurl.Add(b + 1);
            }
            dgPans?.Invalidate();
        }

        void Recalculer()
        {
            piece.NormaliserReprises();
            _load = true;
            tbEtape.Maximum = Math.Max(0, piece.Sequence.Count - 1);
            etape = Math.Max(0, Math.Min(tbEtape.Maximum, etape));
            tbEtape.Value = Math.Min(tbEtape.Maximum, Math.Max(tbEtape.Minimum, etape));
            _load = false;

            ListerSequence();
            Redessiner();
            vuePupitre.Afficher(piece, etape, plieuse, poincon, matrice, atelier.Embase);
            SurlignerPansDuPli(etape);
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

        void NumMachine(Panel p, string lab, double v, ref int y, Action<double> onChange)
        {
            p.Controls.Add(new Label { Text = lab, Left = 24, Top = y + 4, Width = 164, ForeColor = Theme.Discret });
            var n = new NumericUpDown
            {
                Left = 190, Top = y, Width = 148, Minimum = 0, Maximum = 5000,
                DecimalPlaces = 1, Increment = 1, Value = (decimal)v,
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
            p.Controls.Add(n);
            y += 28;
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
