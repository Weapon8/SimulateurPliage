using System;
using System.Collections.Generic;

namespace SimulateurPliage.Materiel
{
    /// <summary>Un vé utilisable sur une matrice (une matrice multi-V en a plusieurs).</summary>
    public sealed class VForm
    {
        public double V = 12;          // ouverture
        public double AngleDeg = 45;
        public double R = 1.5;
        public double Profondeur = 0;  // 0 = déduite de V et de l'angle

        public double ProfondeurReelle =>
            Profondeur > 0 ? Profondeur : (V / 2.0) / Math.Tan(AngleDeg * Math.PI / 360.0);

        public override string ToString() => $"V{V:0.#} · {AngleDeg:0}° · R{R:0.#}";
    }

    public sealed class Matrice
    {
        public string Nom = "Euram / V16";
        public double TeteLargeur = 30;   // largeur de la TÊTE : le vé y est usiné
        public double PiedLargeur = 60;   // largeur du PIED d'appui (talon Promecam, s'aligne sur la table)
        public double PiedHauteur = 20;   // hauteur du pied
        public double Hauteur     = 80;   // hauteur totale sous la face (Euram série 2009 : 80 ou 100)
        public bool MultiV = false;
        public List<VForm> Vs = new();

        public VForm VProche(double v)
        {
            VForm best = null; double bd = double.MaxValue;
            foreach (var x in Vs) { double d = Math.Abs(x.V - v); if (d < bd) { bd = d; best = x; } }
            return best ?? new VForm { V = v };
        }

        /// <summary>
        /// Contour de la matrice, face supérieure à y = 0. Profil en T INVERSÉ (⊥) Euro-style
        /// Amada/Promecam : tête étroite en haut où le vé est usiné, pied large en bas — le
        /// talon d'appui, qui cale la matrice et l'allège (Weapon). Purement visuel : la tôle
        /// ne touche que le vé, le corps n'entre dans aucun calcul de collision.
        /// </summary>
        public List<double[]> Contour(double v)
        {
            var vf = VProche(v);
            if (MultiV) return Contour4Voies(vf);
            double prof = vf.ProfondeurReelle;
            double demiT = Math.Max(TeteLargeur, vf.V + 8) / 2.0;          // la tête doit border le vé
            double demiP = Math.Max(PiedLargeur, demiT * 2) / 2.0;         // le pied déborde la tête
            double h = Math.Max(Hauteur, prof + PiedHauteur + 8);
            double yPied = -(h - PiedHauteur);
            return new List<double[]>
            {
                new[] { -demiT, 0.0 },       // coin haut gauche de la tête
                new[] { -vf.V / 2, 0.0 },    // bord gauche du vé
                new[] { 0.0, -prof },        // fond du vé
                new[] {  vf.V / 2, 0.0 },    // bord droit du vé
                new[] {  demiT, 0.0 },       // coin haut droit
                new[] {  demiT, yPied },     // descente de la tête (droite)
                new[] {  demiP, yPied },     // épaulement du pied (droite)
                new[] {  demiP, -h },        // pied droit
                new[] { -demiP, -h },        // pied gauche
                new[] { -demiP, yPied },     // épaulement du pied (gauche)
                new[] { -demiT, yPied },     // remontée de la tête (gauche)
            };
        }

        /// <summary>
        /// Les vés d'une 4 voies classés du PLUS PETIT au PLUS GRAND — c'est comme ça qu'un
        /// opérateur numérote ses faces : 1 = le petit vé (tôle fine), 4 = le grand. Plus la
        /// tôle est forte, plus on ouvre : le tonnage chute et on n'écrase ni la machine ni
        /// l'outillage. Liste vide si ce n'est pas une 4 voies.
        /// </summary>
        public List<VForm> Faces()
        {
            var l = new List<VForm>();
            if (!MultiV) return l;
            l.AddRange(Vs);
            l.Sort((a, b) => a.V.CompareTo(b.V));
            return l;
        }

        /// <summary>Libellé d'une face pour le sélecteur : « 1 · V16 · 88° · R2 ».</summary>
        public string LibelleFace(int i)
        {
            var f = Faces();
            if (i < 0 || i >= f.Count) return "";
            return $"{i + 1} · V{f[i].V:0.#} · {f[i].AngleDeg:0}° · R{f[i].R:0.#}";
        }

        /// <summary>Le vé de MÊME ANGLE : sur une 4 voies il est sur la face OPPOSÉE.</summary>
        VForm Jumeau(VForm x)
        {
            foreach (var y in Vs)
                if (!ReferenceEquals(y, x) && Math.Abs(y.AngleDeg - x.AngleDeg) < 0.5) return y;
            return null;
        }

        /// <summary>
        /// Matrice 4 VOIES, en section : bloc carré avec un vé usiné sur CHACUNE des quatre
        /// faces — l'X du plan constructeur. On tourne le bloc pour amener le vé voulu en haut.
        /// Les vés de même angle sont sur des faces opposées (85° dessus/dessous, 88° gauche
        /// et droite) : le jumeau du vé actif passe donc en bas, les deux autres sur les côtés.
        ///
        /// Ce n'est pas du décor. C'est l'outil qui permet de plier du fort à 90° : on ouvre le
        /// vé, le tonnage s'effondre, et on n'écrase ni la machine ni l'outillage. Les 85/88°
        /// existent pour pouvoir écraser et rattraper le retour élastique — avec un vé à 90°
        /// on ne ferait jamais un 90°.
        /// </summary>
        List<double[]> Contour4Voies(VForm haut)
        {
            double c = Math.Max(TeteLargeur, PiedLargeur);   // le bloc est carré
            double d = c / 2.0;                              // demi-côté
            double mid = -d;                                 // milieu des faces latérales

            VForm bas = Jumeau(haut);
            VForm gauche = null, droite = null;
            foreach (var x in Vs)
                if (!ReferenceEquals(x, haut) && !ReferenceEquals(x, bas))
                { if (gauche == null) gauche = x; else if (droite == null) droite = x; }

            double P(VForm f) => f == null ? 0 : Math.Min(f.ProfondeurReelle, d - 2);
            double L(VForm f) => f == null ? 0 : Math.Min(f.V, c - 4);

            var p = new List<double[]>();
            // face du HAUT (y = 0), de gauche à droite
            p.Add(new[] { -d, 0.0 });
            if (haut != null) { p.Add(new[] { -L(haut) / 2, 0.0 }); p.Add(new[] { 0.0, -P(haut) }); p.Add(new[] { L(haut) / 2, 0.0 }); }
            p.Add(new[] { d, 0.0 });
            // face de DROITE, en descendant
            if (droite != null) { p.Add(new[] { d, mid + L(droite) / 2 }); p.Add(new[] { d - P(droite), mid }); p.Add(new[] { d, mid - L(droite) / 2 }); }
            p.Add(new[] { d, -c });
            // face du BAS, de droite à gauche
            if (bas != null) { p.Add(new[] { L(bas) / 2, -c }); p.Add(new[] { 0.0, -c + P(bas) }); p.Add(new[] { -L(bas) / 2, -c }); }
            p.Add(new[] { -d, -c });
            // face de GAUCHE, en remontant
            if (gauche != null) { p.Add(new[] { -d, mid - L(gauche) / 2 }); p.Add(new[] { -d + P(gauche), mid }); p.Add(new[] { -d, mid + L(gauche) / 2 }); }
            return p;
        }

        public override string ToString() => Nom;

        public static List<Matrice> Presets() => new()
        {
            new Matrice
            {
                Nom = "2035 / 35°", TeteLargeur = 26, PiedLargeur = 60, Hauteur = 80,
                Vs = { new VForm { V = 8, AngleDeg = 35, R = 1.5 }, new VForm { V = 12, AngleDeg = 35, R = 2.0 } }
            },
            new Matrice
            {
                Nom = "2045 / 45°", TeteLargeur = 30, PiedLargeur = 60, Hauteur = 80,
                Vs =
                {
                    new VForm { V = 10, AngleDeg = 45, R = 1.0 },
                    new VForm { V = 12, AngleDeg = 45, R = 1.5 },
                    new VForm { V = 16, AngleDeg = 45, R = 2.0 },
                    new VForm { V = 20, AngleDeg = 45, R = 2.5 },
                    new VForm { V = 25, AngleDeg = 45, R = 3.0 },
                }
            },
            // Matrice 4 VOIES : bloc carré 60×60, un vé usiné sur chacune des quatre faces,
            // R2, 50-60 HRC. On la tourne pour changer de vé — d'où le bloc carré et pas un ⊥.
            // Cotes relevées sur le plan constructeur (Weapon) : V35 et V50 à 85°, V22 et V16
            // à 88°. Les profondeurs se déduisent de l'ouverture et de l'angle, on ne les force
            // pas : les anciennes valeurs (22/22/16) étaient en fait les OUVERTURES des vés
            // voisins, prises pour des profondeurs. Et le V22 manquait.
            new Matrice
            {
                Nom = "Euram 2009 · 4 voies 60×60", TeteLargeur = 60, PiedLargeur = 60, Hauteur = 60, MultiV = true,
                Vs =
                {
                    new VForm { V = 50, AngleDeg = 85, R = 2.0 },
                    new VForm { V = 35, AngleDeg = 85, R = 2.0 },
                    new VForm { V = 22, AngleDeg = 88, R = 2.0 },
                    new VForm { V = 16, AngleDeg = 88, R = 2.0 },
                }
            },
        };
    }

    /// <summary>Embases porte-outil : elles peuvent entrer en collision avec la pièce.</summary>
    public sealed class Embase
    {
        public double PortePoinconH = 60;
        public double PortePoinconLg = 40;
        public double SemelleH = 25;      // porte-matrice sous le bloc — estimé sur photo (~22)
        public double SemelleLg = 90;
    }
}
