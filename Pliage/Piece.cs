using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SimulateurPliage.Pliage
{
    public enum Sens { Haut, Bas }

    /// <summary>Un point 2D dans le plan de section.</summary>
    public readonly struct Pt
    {
        public readonly double X, Y;
        public Pt(double x, double y) { X = x; Y = y; }
    }

    /// <summary>Une passe de pliage sur une ligne. Plusieurs passes sur la même ligne = reprise.</summary>
    public sealed class Operation
    {
        public int Bend;                  // index de la ligne de pli
        public double AngleCible = 90;    // angle intérieur visé (180 = plat)
        public Sens Sens = Sens.Haut;
        public double V = 16;             // ouverture matrice
        public bool Reprise;              // pli en plusieurs passes
        public bool ButeeAval;            // rotation 180° À PLAT (bout pour bout) : la butée lit le pan aval
        public bool Retournee;            // retournement DESSUS/DESSOUS : les plis déjà faits pointent en bas
    }

    /// <summary>La tôle : ses pans, son épaisseur, sa séquence de pliage.</summary>
    public sealed class Piece
    {
        public string Nom = "";                  // nom du profil (éditable, sauvegardé)
        public string Chantier = "";             // regroupement optionnel : suite de chantier / paquet
        public double Epaisseur = 1.0;
        public double LongueurPli = 500;
        public double Rm = 450;                  // N/mm² : acier 450, inox 600, alu 250, zinc 150
        public bool CotesExterieures = false;
        public List<double> Segments = new();    // NbPlis + 1 pans

        // ---- FORME CIBLE (un par pli) : le solveur la lit et produit la séquence ----

        /// <summary>Angle intérieur visé. 180 = reste à plat.</summary>
        public List<double> Angles = new();

        /// <summary>Face dessus au moment du pli. false = FNL (référence) · true = FL.
        /// Même face = même sens de virage. C'est ça qui fait la forme.</summary>
        public List<bool> Faces = new();

        public List<Operation> Sequence = new();

        /// <summary>
        /// PIÈCES COMPLEXES — les bandes des autres axes de pliage. Vide sur une pièce simple :
        /// un profil de couverture n'a qu'un axe, tous ses plis sont parallèles.
        /// Une boîte en a deux : quatre plis, on tourne la pièce d'un quart de tour, quatre plis.
        /// Chaque axe est une bande complète, avec ses pans, ses angles et sa séquence.
        /// Ils se calculent séparément : l'outillage est prismatique et segmentable, donc les
        /// parois relevées d'un axe sortent du plan de section de l'autre et ne le gênent pas.
        /// </summary>
        public List<Piece> AxesSecondaires = new();

        [JsonIgnore] public bool Complexe => AxesSecondaires.Count > 0;
        [JsonIgnore] public int NbAxes => 1 + AxesSecondaires.Count;

        /// <summary>Tous les axes, celui-ci d'abord. Une pièce simple se rend elle-même.</summary>
        public List<Piece> TousLesAxes()
        {
            var l = new List<Piece> { this };
            l.AddRange(AxesSecondaires);
            return l;
        }

        [JsonIgnore] public int NbPlis => Math.Max(0, Segments.Count - 1);
        [JsonIgnore] public double Developpe { get { double t = 0; foreach (var s in Segments) t += s; return t; } }

        /// <summary>Cote butée, toujours exprimée en intérieur (comme une CN Cybelec/Delem).</summary>
        public double ButeeInt(int i)
        {
            if (i < 0 || i >= Segments.Count) return 0;
            double L = Segments[i];
            if (CotesExterieures) L -= Epaisseur;
            return Math.Max(0, L);
        }

        public void SetButeeInt(int i, double r)
        {
            if (i < 0 || i >= Segments.Count) return;
            Segments[i] = CotesExterieures ? Math.Max(0, r) + Epaisseur : Math.Max(0, r);
        }

        /// <summary>Une entrée d'Angles et de Faces par pli. Si une séquence existe
        /// (démo, fichier ancien), elle fait foi : on en déduit la forme.</summary>
        public void AssurerForme()
        {
            int n = NbPlis;
            while (Angles.Count < n) Angles.Add(90);
            while (Angles.Count > n) Angles.RemoveAt(Angles.Count - 1);
            while (Faces.Count < n) Faces.Add(false);
            while (Faces.Count > n) Faces.RemoveAt(Faces.Count - 1);

            foreach (var o in Sequence)
                if (o.Bend >= 0 && o.Bend < n) { Angles[o.Bend] = o.AngleCible; Faces[o.Bend] = o.Retournee; }
        }

        /// <summary>Garantit au moins nb lignes de pli (donc nb+1 pans).</summary>
        public void AssurerPlis(int nb)
        {
            int segs = Math.Max(2, nb + 1);
            while (Segments.Count < segs) Segments.Add(100);
        }

        /// <summary>
        /// Retournements de la pièce. Le premier pli fixe la face de référence ;
        /// tout changement de sens impose de retourner la pièce.
        /// </summary>
        public bool[] Retournements()
        {
            var f = new bool[Sequence.Count];
            if (Sequence.Count == 0) return f;
            Sens courant = Sequence[0].Sens;
            for (int i = 1; i < Sequence.Count; i++)
            {
                if (Sequence[i].Sens != courant) { f[i] = true; courant = Sequence[i].Sens; }
            }
            return f;
        }

        /// <summary>
        /// Marque comme reprise toute passe dont la ligne de pli a déjà été formée
        /// par une opération antérieure de la séquence. Une reprise ne référence pas
        /// la butée comme un premier pli : le pan est déjà plié, la passe re-frappe.
        /// C'est une donnée STRUCTURELLE, déduite de la séquence, pas un choix libre.
        /// </summary>
        public void NormaliserReprises()
        {
            var vus = new HashSet<int>();
            foreach (var o in Sequence)
            {
                o.Reprise = vus.Contains(o.Bend);
                vus.Add(o.Bend);
            }
        }

        /// <summary>
        /// Index de la ligne de pli qui vient EN APPUI contre le doigt de butée à l'étape s,
        /// ou -1 si c'est un simple bord de tôle qui touche.
        ///
        /// La butée lit le pan amont (ou l'aval si la pièce est présentée bout pour bout).
        /// Si ce pan porte à son extrémité un pli DÉJÀ formé, c'est ce retour qui vient contre
        /// le doigt — pas le bord brut. C'est le « pli à la butée » de l'opérateur : le 25 qui
        /// s'appuie sur le retour du 10, le 40 qui bute contre le retour du 20, le 100 contre
        /// celui du 40. Ce n'est PAS un retournement : on pousse, c'est tout.
        /// </summary>
        public int PliAppui(int s)
        {
            if (s < 0 || s >= Sequence.Count) return -1;
            var op = Sequence[s];
            int voisin = op.ButeeAval ? op.Bend + 1 : op.Bend - 1;
            if (voisin < 0 || voisin >= NbPlis) return -1;
            for (int i = 0; i < s; i++)
                if (Sequence[i].Bend == voisin) return voisin;   // déjà formé => il fait l'appui
            return -1;
        }

        /// <summary>Angle intérieur d'une ligne juste avant l'étape s.</summary>
        public double AngleAvant(int s)
        {
            if (s < 0 || s >= Sequence.Count) return 180;
            int bend = Sequence[s].Bend;
            double a = 180;
            for (int i = 0; i < s; i++)
                if (Sequence[i].Bend == bend) a = Sequence[i].AngleCible;
            return a;
        }

        /// <summary>
        /// Chevêtre de référence : aile 20 · joue 40 · fond 100 · joue 40 · aile 20.
        /// Séquence opérateur réelle, dans l'ordre d'exécution :
        ///   1) pli 1 (bend 0) direct           → butée 20
        ///   2) pli 4 (bend 3) ⇄ retourné à plat → butée 20
        ///   3) pli 2 (bend 1) direct           → butée 40
        ///   4) pli 3 (bend 2) direct           → butée 100 (se cale sur le 40, lit le fond)
        /// Les 4 plis vont vers le HAUT, tous à 90°.
        /// </summary>
        public static Piece Demo()
        {
            var p = new Piece { Epaisseur = 1.0, Nom = "Chevêtre 20·40·100·40·20" };
            p.Segments.AddRange(new double[] { 20, 40, 100, 40, 20 });
            p.Sequence.Add(new Operation { Bend = 0, AngleCible = 90, Sens = Sens.Haut, V = 16 });
            p.Sequence.Add(new Operation { Bend = 3, AngleCible = 90, Sens = Sens.Haut, V = 16, ButeeAval = true });
            p.Sequence.Add(new Operation { Bend = 1, AngleCible = 90, Sens = Sens.Haut, V = 16 });
            p.Sequence.Add(new Operation { Bend = 2, AngleCible = 90, Sens = Sens.Haut, V = 16 });
            return p;
        }

        /// <summary>
        /// Z laqué de référence : dos 30 · assise 25 · façade 25 · retour 10.
        /// Développé S1=30 · S2=25 · S3=25 · S4=10 (bends 0/1/2).
        /// La face de référence est la FNL (non laquée), gardée dessus : le laquage
        /// reste protégé dessous pendant les 2 premiers plis, puis on retourne.
        /// Séquence opérateur réelle, dans l'ordre d'exécution — TOUS les volets montent :
        ///   1) pli du 10 (bend 2) à 45° en butée AVAL ⇄ — FNL dessus. RÈGLE OPÉRATEUR
        ///      (Weapon) : toujours le plus grand côté vers l'opérateur. Le 10 part donc à la
        ///      butée (le doigt lit 10) et on garde les 80 en main — 10 mm entre les doigts,
        ///      c'est non. Séquence confirmée par le solveur, pas choisie à la main.
        ///   2) pli du 25 (bend 1) à 92° en butée AVAL ⇄ — FNL dessus. Le retour du 10 déjà plié
        ///      doit se loger dans le DÉGAGEMENT DU COL DE CYGNE (flanc droit, 2,8 mm à y=17,5) ;
        ///      côté opérateur le corps fait 8,7 mm et le retour (pointe à 8,0) taperait dedans.
        ///      C'est lui qui vient à l'appui — mesuré dans le code, pas supposé.
        ///   3) ⇅ dessus/dessous, puis pli du 30 (bend 0) à 90° en butée AMONT
        ///      → la face laquée passe dessus (côté montre), la façade descend du bon côté.
        /// Le ⇅ (Retournee) inverse le sens des plis déjà faits : c'est lui qui met le 30
        /// à l'opposé du 25 façade. Butée amont (et non aval) : le déjà-plié tombe côté
        /// opérateur et dégage le doigt de butée — sens d'engagement confirmé par le solveur.
        /// NB : l'appui « contre le retour du 10 » n'est pas encore un mode de butée à part —
        /// la cote R affichée reste le modèle 1 pan (à raffiner avec l'appui-sur-pli-formé).
        /// </summary>
        public static Piece DemoZLaque()
        {
            var p = new Piece { Epaisseur = 1.0, Nom = "Z laqué 30·25·25·10" };
            p.Segments.AddRange(new double[] { 30, 25, 25, 10 });
            p.Sequence.Add(new Operation { Bend = 2, AngleCible = 45, Sens = Sens.Haut, V = 16, ButeeAval = true });
            p.Sequence.Add(new Operation { Bend = 1, AngleCible = 92, Sens = Sens.Haut, V = 16, ButeeAval = true });
            p.Sequence.Add(new Operation { Bend = 0, AngleCible = 90, Sens = Sens.Haut, V = 16, Retournee = true });
            return p;
        }

        /// <summary>
        /// Couvertine de chantier — cotes et gamme de Weapon, elles FONT FOI.
        /// pince 10 · jambe 30 · fond 230 · jambe 30 · goutte d'eau 10
        ///
        ///   op1  le 10  ·  45°  FNL              butée 10 · prise 300
        ///   op2  le 30  ·  92°  FNL, appui pli 1 butée 30 · prise 270
        ///   op3  le 10  · 163°  FL  ⇅            butée 10 · prise 300
        ///   op4  le 30  ·  88°  FNL ⇅, appui op3 butée 30 · prise 270
        /// </summary>
        public static Piece DemoCouvertine()
        {
            var p = new Piece { Epaisseur = 1.0, Nom = "Couvertine 10·30·230·30·10" };
            p.Segments.AddRange(new double[] { 10, 30, 230, 30, 10 });
            p.Sequence.Add(new Operation { Bend = 0, AngleCible = 45,  Sens = Sens.Haut, V = 16 });
            p.Sequence.Add(new Operation { Bend = 1, AngleCible = 92,  Sens = Sens.Haut, V = 16 });
            p.Sequence.Add(new Operation { Bend = 3, AngleCible = 163, Sens = Sens.Haut, V = 16, ButeeAval = true, Retournee = true });
            p.Sequence.Add(new Operation { Bend = 2, AngleCible = 88,  Sens = Sens.Haut, V = 16, ButeeAval = true });
            return p;
        }
    }

    /// <summary>Une collision détectée à une étape.</summary>
    public sealed class Collision
    {
        public string Type;
        public string Detail;
        public bool Bloquant;
        public Collision(string type, string detail, bool bloquant)
        { Type = type; Detail = detail; Bloquant = bloquant; }
    }

    /// <summary>Géométrie de la pièce à une étape donnée, dans le repère ancré sur le pli actif.</summary>
    public sealed class EtatEtape
    {
        public int Etape;
        public Operation Op;
        public List<Pt> PanArriere = new();   // côté butée, posé sur la matrice
        public List<Pt> Formage = new();       // volet en cours de formage
        public double ButeeDistance;
        public List<Collision> Collisions = new();

        public bool Bloque => Collisions.Exists(c => c.Bloquant);
    }
}
