using System;
using System.Collections.Generic;

namespace SimulateurPliage.Pliage
{
    /// <summary>
    /// Géométrie du pliage. Repère de travail : le sommet du pli actif est à l'origine,
    /// le pan côté butée est couché le long de -X, le volet en formage monte vers +Y.
    /// Ne dépend d'aucune UI.
    /// </summary>
    public static class Moteur
    {
        const double D2R = Math.PI / 180.0;

        /// <summary>Angles intérieurs de chaque ligne après les étapes 0..etape.</summary>
        public static double[] AnglesA(Piece p, int etape, out Sens[] sens)
        {
            // Sur une pièce COMPLEXE, la séquence est globale et alterne les axes : les plis
            // de l'axe Y ne courbent pas la bande X. On ne garde que ceux de l'axe actif.
            int axe = (etape >= 0 && etape < p.Sequence.Count) ? p.Sequence[etape].Axe : 0;
            var bande = p.Bande(axe);
            int nb = bande.NbPlis;
            var ang = new double[Math.Max(0, nb)];
            sens = new Sens[Math.Max(0, nb)];
            for (int i = 0; i < nb; i++) { ang[i] = 180.0; sens[i] = Sens.Haut; }

            for (int i = 0; i <= etape && i < p.Sequence.Count; i++)
            {
                var op = p.Sequence[i];
                if (op.Axe != axe) continue;                 // pli d'un autre axe : il ne courbe pas cette bande
                if (op.Bend >= 0 && op.Bend < nb) { ang[op.Bend] = op.AngleCible; sens[op.Bend] = op.Sens; }
            }
            return ang;
        }

        /// <summary>Chaîne de la fibre neutre, pliée selon les angles donnés.</summary>
        public static List<Pt> Chaine(List<double> segs, double[] ang, Sens[] sens)
        {
            var pts = new List<Pt> { new Pt(0, 0) };
            double dir = 0, x = 0, y = 0;

            for (int i = 0; i < segs.Count; i++)
            {
                x += segs[i] * Math.Cos(dir);
                y += segs[i] * Math.Sin(dir);
                pts.Add(new Pt(x, y));

                if (i < segs.Count - 1 && i < ang.Length)
                    dir += (sens[i] == Sens.Haut ? +1 : -1) * (180.0 - ang[i]) * D2R;
            }
            return pts;
        }

        /// <summary>Construit l'état géométrique d'une étape, collisions comprises.</summary>
        public static EtatEtape Construire(Piece p, int etape, Materiel.Plieuse plieuse,
                                           Materiel.Poincon poincon, Materiel.Matrice matrice, Materiel.Embase embase)
        {
            var st = new EtatEtape { Etape = etape };
            if (p.Sequence.Count == 0 || etape < 0 || etape >= p.Sequence.Count) return st;

            st.Op = p.Sequence[etape];
            var ang = AnglesA(p, etape, out var sens);

            // LE SENS D'UN PLI DEPEND DE SA FACE — pas du drapeau de l'etape courante.
            //
            // Un pli forme sur la face opposee apparait INVERSE des qu'on revient sur la face de
            // reference. L'ancien code ne regardait que st.Op.Retournee et inversait tout le
            // monde ou personne : il ratait le cas d'un retournement SUIVI d'un re-retournement.
            // La couvertine, precisement — le 10 a 163° fait en FL, puis on revient en FNL pour
            // le 30 a 88° : a cette etape-la, le 163° etait dessine du mauvais cote. (Weapon)
            //
            // Le pli ACTIF, lui, monte toujours : la presse descend, la matrice tient.
            int axeAct = st.Op.Axe;
            // Si les FACES sont renseignées (pièce lue/saisie), le SENS de chaque pli suit la
            // FACE réelle : un pli sur la face de référence (FNL) tourne d'un côté, un pli sur
            // la face opposée (FL) tourne de l'autre. C'est ce qui donne la vraie forme (le U
            // du chéneau) au lieu d'un zigzag. Le pli ACTIF monte toujours (Sens.Haut).
            // Le côté de référence = la face du pli actif : un pli de MÊME face que l'actif est
            // dessiné dans le même sens, un pli de face OPPOSÉE en sens inverse.
            bool facesConnues = p.FacesManuelles && st.Op.Bend < p.Faces.Count;
            bool faceAct = facesConnues && p.Faces[st.Op.Bend];
            for (int i = 0; i <= etape && i < p.Sequence.Count; i++)
            {
                var o = p.Sequence[i];
                if (o.Axe != axeAct || o.Bend < 0 || o.Bend >= sens.Length) continue;
                if (o.Bend == st.Op.Bend) { sens[o.Bend] = Sens.Haut; continue; }  // pli actif monte
                if (facesConnues && o.Bend < p.Faces.Count)
                    // sens selon la FACE : même face que l'actif -> Haut, face opposée -> Bas
                    sens[o.Bend] = (p.Faces[o.Bend] == faceAct) ? Sens.Haut : Sens.Bas;
                else
                    // legacy : sens selon le drapeau de retournement
                    sens[o.Bend] = (o.Retournee == st.Op.Retournee) ? Sens.Haut : Sens.Bas;
            }

            var bande = p.Bande(st.Op.Axe);
            var chaine = Chaine(bande.Segments, ang, sens);

            int sommet = st.Op.Bend + 1;
            if (sommet < 1 || sommet >= chaine.Count) return st;

            Ancrer(chaine, sommet, st.Op.ButeeAval);

            // Convention d'affichage FIXE : butée + pan couché à DROITE, opérateur + formage
            // à GAUCHE, quelle que soit l'étape. Ancrer met déjà le pan côté butée à droite ;
            // ici on range PanArriere = pan couché (butée), Formage = côté opérateur, en
            // partant toujours du sommet vers l'extérieur. Sans ça, un ⇄ inversait l'image
            // (le formage passait à droite, côté butée).
            if (!st.Op.ButeeAval)
            {
                for (int i = 0; i <= sommet; i++) st.PanArriere.Add(chaine[i]);          // amont couché
                for (int i = sommet; i < chaine.Count; i++) st.Formage.Add(chaine[i]);   // aval opérateur
            }
            else
            {
                for (int i = chaine.Count - 1; i >= sommet; i--) st.PanArriere.Add(chaine[i]); // aval couché
                for (int i = sommet; i >= 0; i--) st.Formage.Add(chaine[i]);                   // amont opérateur, sommet→ext
            }

            // la butee lit le pan couche contre elle : l'amont, ou l'aval si rotation a plat
            // COTE DE BUTÉE.
            // Si les FACES sont renseignées (FacesManuelles, pièce lue au dessin / saisie),
            // on applique la règle métier Weapon fondée sur la face : un pli INTÉRIEUR (FNL)
            // s'appuie sur le retour précédent -> butée lit le pan AVAL ; un pli EXTÉRIEUR (FL,
            // pièce retournée ⇅) -> butée lit l'AMONT ; le retournement à plat ⇄ inverse.
            // Ça donne la vraie gamme du chéneau : 10 · 100 · 30 · 40 · 200.
            // Si les faces NE sont PAS définies (anciennes démos, fichiers legacy), on garde le
            // comportement d'origine (pan aval, ⇄ -> amont) pour ne rien casser.
            int panButee;
            if (bande.FacesManuelles)
            {
                bool litAmont = st.Op.Bend < bande.Faces.Count && bande.Faces[st.Op.Bend];  // FL -> amont
                if (st.Op.ButeeAval) litAmont = !litAmont;                                   // ⇄ inverse
                panButee = litAmont ? st.Op.Bend : st.Op.Bend + 1;
            }
            else
            {
                panButee = st.Op.ButeeAval ? st.Op.Bend + 1 : st.Op.Bend;   // legacy (code d'origine)
            }
            st.ButeeDistance = bande.ButeeInt(Math.Min(panButee, bande.Segments.Count - 1));
            st.Collisions = Detecteur.Analyser(st, p, plieuse, poincon, matrice, embase);

            // NB : en position de pose, le pli actif est encore à plat (180°). La tôle
            // pose donc TOUJOURS à plat sur la matrice, les retours déjà formés se
            // présentent en l'air (le 40 bute contre le retour du 20, le 100 contre celui
            // du 40). Il n'y a pas de « plongeon sous la matrice » à détecter ici : c'est
            // au solveur d'ordre auto de juger la faisabilité d'un enchaînement, pas à un
            // faux blocage sur une séquence saisie à la main.

            return st;
        }

        /// <summary>
        /// Vrai si, en position de pose (pli actif encore a 180, plis anterieurs formes),
        /// le pan de reference (cote butee) reste au-dessus de la face matrice — donc
        /// peut se coucher a plat et venir contre le doigt. Modele valide sur le chevetre
        /// (l'ordre 1-2-3-4 direct plonge a -60/-120, rejete) et sur le Z 30/25/25/10.
        /// </summary>
        /// <summary>
        /// Place le sommet actif a l'origine et aligne la BISSECTRICE du pli sur +Y
        /// (l'axe du poincon) : les deux ailes s'ecartent symetriquement autour du bec.
        /// RÈGLE FIGÉE : le pan qu'on gauche CONTRE LA BUTÉE va TOUJOURS à DROITE (côté
        /// butée), quelle que soit sa taille — sinon la butée ne sert à rien. Le reste
        /// part à gauche (opérateur).
        ///   ButeeAval = false : on pousse en butee, la butee lit le pan AMONT.
        ///   ButeeAval = true  : la piece est retournee BOUT POUR BOUT (rotation a plat,
        ///                       la face ne change pas), la butee lit le pan AVAL.
        /// C'est ce retournement qui rend faisable un chevetre : on ne bute jamais sur l'ame.
        /// A 180° la bissectrice est indefinie : on couche le pan a l'horizontale.
        /// </summary>
        static void Ancrer(List<Pt> chaine, int sommet, bool buteeAval)
        {
            Pt o = chaine[sommet];
            for (int i = 0; i < chaine.Count; i++)
                chaine[i] = new Pt(chaine[i].X - o.X, chaine[i].Y - o.Y);

            Pt u1 = Unitaire(chaine[sommet - 1]);
            Pt u2 = sommet + 1 < chaine.Count ? Unitaire(chaine[sommet + 1]) : new Pt(-u1.X, -u1.Y);

            double bx = u1.X + u2.X, by = u1.Y + u2.Y;
            if (Math.Sqrt(bx * bx + by * by) < 1e-6) { bx = -u1.Y; by = u1.X; }   // pli a plat
            Pt b = Unitaire(new Pt(bx, by));

            double rot = Math.PI / 2 - Math.Atan2(b.Y, b.X);
            double cs = Math.Cos(rot), sn = Math.Sin(rot);
            for (int i = 0; i < chaine.Count; i++)
                chaine[i] = new Pt(chaine[i].X * cs - chaine[i].Y * sn,
                                   chaine[i].X * sn + chaine[i].Y * cs);

            // RÈGLE DE SENS FIGÉE (Weapon) : le GRAND côté va à GAUCHE (opérateur), le petit
            // à DROITE (butée). L'opérateur tient toujours le plus grand pan devant lui ; la
            // butée cale le petit. On oriente donc d'après la PORTÉE HORIZONTALE de chaque côté
            // du sommet (pas d'après un pan de butée fixe) : le côté qui s'étend le plus loin
            // doit finir à gauche (X négatif).
            double porteeGauche = 0, porteeDroite = 0;
            foreach (var pt in chaine)
            {
                if (pt.X < -porteeGauche) porteeGauche = -pt.X;
                if (pt.X > porteeDroite) porteeDroite = pt.X;
            }
            // si le grand côté est à droite, on miroir pour le ramener à gauche.
            if (porteeDroite > porteeGauche)
                for (int i = 0; i < chaine.Count; i++)
                    chaine[i] = new Pt(-chaine[i].X, chaine[i].Y);
        }

        static Pt Unitaire(Pt p)
        {
            double m = Math.Sqrt(p.X * p.X + p.Y * p.Y);
            return m > 1e-9 ? new Pt(p.X / m, p.Y / m) : new Pt(0, 0);
        }
    }
}
