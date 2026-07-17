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

            // Retournement dessus/dessous : la piece repose sur l'autre face, donc les plis
            // DEJA FORMES pointent a l'oppose. Mais le pli ACTIF, lui, se fait toujours vers le
            // haut (le poincon descend, la matrice tient) : son sens ne s'inverse JAMAIS avec le
            // retournement. Inverser tout le monde repliait le volet actif dans le meme sens que
            // le deja-plie -> spirale collee au poincon. On saute donc le pli actif.
            if (st.Op.Retournee)
                for (int i = 0; i < sens.Length; i++)
                    if (i != st.Op.Bend)
                        sens[i] = sens[i] == Sens.Haut ? Sens.Bas : Sens.Haut;

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
            int panButee = st.Op.ButeeAval ? st.Op.Bend + 1 : st.Op.Bend;
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
        public static bool ButeeAPlat(Piece p, int etape, out string raison)
        {
            raison = null;
            if (etape < 0 || etape >= p.Sequence.Count) return true;
            var op = p.Sequence[etape];
            int nb = p.NbPlis;
            if (nb <= 0) return true;

            // angles de positionnement : plis DEJA faits (etapes < etape) appliques,
            // pli actif et suivants a plat (180).
            var ang = new double[nb];
            var sens = new Sens[nb];
            for (int i = 0; i < nb; i++) { ang[i] = 180.0; sens[i] = Sens.Haut; }
            for (int i = 0; i < etape && i < p.Sequence.Count; i++)
            {
                var o = p.Sequence[i];
                if (o.Bend >= 0 && o.Bend < nb) { ang[o.Bend] = o.AngleCible; sens[o.Bend] = o.Sens; }
            }
            if (op.Retournee)
                for (int i = 0; i < nb; i++) sens[i] = sens[i] == Sens.Haut ? Sens.Bas : Sens.Haut;

            var ch = Chaine(p.Segments, ang, sens);
            int sommet = op.Bend + 1;
            if (sommet < 1 || sommet >= ch.Count) return true;
            Ancrer(ch, sommet, op.ButeeAval);

            // cote reference : aval [sommet..fin] si rotation a plat, sinon amont [0..sommet]
            double miny = double.MaxValue;
            if (op.ButeeAval)
                for (int i = sommet; i < ch.Count; i++) miny = Math.Min(miny, ch[i].Y);
            else
                for (int i = 0; i <= sommet; i++) miny = Math.Min(miny, ch[i].Y);

            if (miny < -0.5)
            {
                raison = "le pan de référence ne pose pas à plat sur la matrice (côté butée sous la face)";
                return false;
            }
            return true;
        }

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

            // Règle de sens : le pan gauché CONTRE LA BUTÉE part à DROITE (X > 0),
            // quelle que soit sa taille ; tout le reste part à gauche (opérateur).
            // Butée = pan amont (direct) ou aval (⇄ retourné à plat).
            int refPan = buteeAval && sommet + 1 < chaine.Count ? sommet + 1 : sommet - 1;
            if (chaine[refPan].X < 0)
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
