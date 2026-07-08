// ============================================================================
//  PATCH Tools.cs — remplace la classe Poincon par celle-ci.
//  + dans ToolLib : passe   public const int CURRENT_VERSION = 7;  ->  = 8;
//    (sinon la biblio outils.json en cache garde l'ancien profil "1012").
//  Le poincon par defaut (new Poincon()) devient le Rolleri P.120.35.R3.
//  Il est COMMUN aux deux machines (Loire Safe 4 m + Amada 2 m) : il vit donc
//  dans la biblio d'outils partagee, pas dans le profil machine.
// ============================================================================

public sealed class Poincon
{
    public string Nom = "Rolleri P.120.35.R3";
    public double Hauteur      = 120;   // hauteur totale (fiche)
    public double HauteurUtile = 90;    // pointe -> epaulement (fiche : "hauteur utile")
    public double AngleDeg     = 35;    // angle de pointe (fiche)
    public double R            = 3.0;   // rayon en pointe R3 (fiche)
    public double CorpsLg      = 26;    // largeur du corps (fiche : 26/1.02")
    public double EpaulementDuHaut = 30; // 120 - 90

    // Poincon DROIT (Serie CLASSIC, type R1) : PAS de col de cygne.
    public double ColRetrait = 0;
    public double ColHauteur = 0;

    // Profil du P.120.35.R3 : (hauteur depuis la pointe ; demi-largeur mm).
    //  Lame ELANCEE, conforme au plan Rolleri :
    //  - pointe R3 avec cone 35° (17,5°/cote) sur ~20 mm (pointe travaillante),
    //  - col de lame fin qui remonte doucement jusqu'au corps 26,
    //  - corps droit jusqu'a la hauteur utile (90),
    //  - queue R1 au-dessus (schematique, cote ram, sans incidence collision tole).
    // Cotes VERROUILLEES par la fiche : pointe 35°, corps 26, utile 90, H 120, R3.
    // Cotes LUES sur le plan (a confirmer au pied a coulisse) : la fin du cone 35°
    //  (20 ; 6.6) et le raccord au corps (65 ; 13.0) — les 2 seules non publiees.
    public List<double[]> Profil = new()
    {
        new[] {   0.0,  0.3 },   // pointe R3
        new[] {  20.0,  6.6 },   // fin du cone 35° (pointe travaillante)
        new[] {  65.0, 13.0 },   // col de lame elance -> corps 26 (13 = 26/2)
        new[] {  90.0, 13.0 },   // corps droit jusqu'a la hauteur utile
        new[] {  95.0,  9.0 },   // epaulement -> queue R1 (schematique)
        new[] { 120.0,  9.0 },   // queue
    };

    // demi-largeur interpolee a la hauteur y (0 = pointe)
    public double DemiLargeur(double y)
    {
        if (Profil == null || Profil.Count == 0) return CorpsLg / 2.0;
        if (y <= Profil[0][0]) return Profil[0][1];
        for (int i = 1; i < Profil.Count; i++)
        {
            if (y <= Profil[i][0])
            {
                double y0 = Profil[i - 1][0], y1 = Profil[i][0];
                double x0 = Profil[i - 1][1], x1 = Profil[i][1];
                double t = (y1 - y0) > 1e-6 ? (y - y0) / (y1 - y0) : 0;
                return x0 + (x1 - x0) * t;
            }
        }
        return Profil[Profil.Count - 1][1];
    }

    // Contour ferme du poincon (pointe en bas a l'origine, monte en +Y).
    public List<double[]> Contour()
    {
        double H = Hauteur;
        var right = new List<double[]>();
        for (double y = 0; y <= H + 0.001; y += 1.5) right.Add(new[] { DemiLargeur(y), y });
        var c = new List<double[]>();
        foreach (var p in right) c.Add(p);
        for (int i = right.Count - 1; i >= 0; i--) c.Add(new[] { -right[i][0], right[i][1] });
        return c;
    }

    public override string ToString() => Nom;
}
