using System;

namespace SimulateurPliage.Pliage
{
    /// <summary>
    /// Les pièces de RÉFÉRENCE de l'atelier, figées en JSON — SORTIES DU CODE EN DUR.
    /// C'est l'étalon : chevêtre, Z laqué, couvertine, chéneau. Elles sont validées à
    /// l'atelier ; leur géométrie, faces et gammes ne doivent PLUS bouger. Si une référence
    /// doit changer, on régénère ce JSON à part — on ne retouche pas le moteur pour elle.
    /// La bibliothèque lit ce texte au démarrage et réinjecte les références manquantes.
    /// </summary>
    public static class ProduitReference
    {
        public const string Json = @"{
  ""Version"": 1,
  ""Profils"": [
    {
      ""Nom"": ""Chevêtre 20·40·100·40·20"",
      ""Chantier"": ""Références"",
      ""Date"": ""2026-07-19"",
      ""Piece"": {
        ""Nom"": ""Chevêtre 20·40·100·40·20"",
        ""Chantier"": """",
        ""Epaisseur"": 1,
        ""LongueurPli"": 500,
        ""Rm"": 450,
        ""CotesExterieures"": false,
        ""Segments"": [
          20,
          40,
          100,
          40,
          20
        ],
        ""Angles"": [
          90,
          90,
          90,
          90
        ],
        ""Faces"": [
          false,
          false,
          false,
          false
        ],
        ""FacesManuelles"": false,
        ""Sequence"": [
          {
            ""Bend"": 0,
            ""AngleCible"": 90,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": false,
            ""Retournee"": false,
            ""Axe"": 0
          },
          {
            ""Bend"": 3,
            ""AngleCible"": 90,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": true,
            ""Retournee"": false,
            ""Axe"": 0
          },
          {
            ""Bend"": 1,
            ""AngleCible"": 90,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": false,
            ""Retournee"": false,
            ""Axe"": 0
          },
          {
            ""Bend"": 2,
            ""AngleCible"": 90,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": false,
            ""Retournee"": false,
            ""Axe"": 0
          }
        ],
        ""AxesSecondaires"": []
      }
    },
    {
      ""Nom"": ""Z laqué 30·25·25·10"",
      ""Chantier"": ""Références"",
      ""Date"": ""2026-07-19"",
      ""Piece"": {
        ""Nom"": ""Z laqué 30·25·25·10"",
        ""Chantier"": """",
        ""Epaisseur"": 1,
        ""LongueurPli"": 500,
        ""Rm"": 450,
        ""CotesExterieures"": false,
        ""Segments"": [
          30,
          25,
          25,
          10
        ],
        ""Angles"": [
          90,
          92,
          45
        ],
        ""Faces"": [
          true,
          false,
          false
        ],
        ""FacesManuelles"": false,
        ""Sequence"": [
          {
            ""Bend"": 2,
            ""AngleCible"": 45,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": true,
            ""Retournee"": false,
            ""Axe"": 0
          },
          {
            ""Bend"": 1,
            ""AngleCible"": 92,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": true,
            ""Retournee"": false,
            ""Axe"": 0
          },
          {
            ""Bend"": 0,
            ""AngleCible"": 90,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": false,
            ""Retournee"": true,
            ""Axe"": 0
          }
        ],
        ""AxesSecondaires"": []
      }
    },
    {
      ""Nom"": ""Couvertine 10·30·230·30·10"",
      ""Chantier"": ""Références"",
      ""Date"": ""2026-07-19"",
      ""Piece"": {
        ""Nom"": ""Couvertine 10·30·230·30·10"",
        ""Chantier"": """",
        ""Epaisseur"": 1,
        ""LongueurPli"": 500,
        ""Rm"": 450,
        ""CotesExterieures"": false,
        ""Segments"": [
          10,
          30,
          230,
          30,
          10
        ],
        ""Angles"": [
          45,
          92,
          88,
          163
        ],
        ""Faces"": [
          false,
          false,
          false,
          true
        ],
        ""FacesManuelles"": false,
        ""Sequence"": [
          {
            ""Bend"": 0,
            ""AngleCible"": 45,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": false,
            ""Retournee"": false,
            ""Axe"": 0
          },
          {
            ""Bend"": 1,
            ""AngleCible"": 92,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": false,
            ""Retournee"": false,
            ""Axe"": 0
          },
          {
            ""Bend"": 3,
            ""AngleCible"": 163,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": true,
            ""Retournee"": true,
            ""Axe"": 0
          },
          {
            ""Bend"": 2,
            ""AngleCible"": 88,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": true,
            ""Retournee"": false,
            ""Axe"": 0
          }
        ],
        ""AxesSecondaires"": []
      }
    },
    {
      ""Nom"": ""Chéneau 30·40·150·200·100·10"",
      ""Chantier"": ""Références"",
      ""Date"": ""2026-07-19"",
      ""Piece"": {
        ""Nom"": ""Chéneau 30·40·150·200·100·10"",
        ""Chantier"": """",
        ""Epaisseur"": 1,
        ""LongueurPli"": 500,
        ""Rm"": 450,
        ""CotesExterieures"": false,
        ""Segments"": [
          30,
          40,
          150,
          200,
          100,
          10
        ],
        ""Angles"": [
          90,
          90,
          90,
          90,
          45
        ],
        ""Faces"": [
          false,
          true,
          false,
          false,
          false
        ],
        ""FacesManuelles"": true,
        ""Sequence"": [
          {
            ""Bend"": 4,
            ""AngleCible"": 45,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": false,
            ""Retournee"": false,
            ""Axe"": 0
          },
          {
            ""Bend"": 3,
            ""AngleCible"": 90,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": false,
            ""Retournee"": false,
            ""Axe"": 0
          },
          {
            ""Bend"": 0,
            ""AngleCible"": 90,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": true,
            ""Retournee"": false,
            ""Axe"": 0
          },
          {
            ""Bend"": 1,
            ""AngleCible"": 90,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": false,
            ""Retournee"": true,
            ""Axe"": 0
          },
          {
            ""Bend"": 2,
            ""AngleCible"": 90,
            ""Sens"": ""Haut"",
            ""V"": 16,
            ""Reprise"": false,
            ""ButeeAval"": false,
            ""Retournee"": true,
            ""Axe"": 0
          }
        ],
        ""AxesSecondaires"": []
      }
    }
  ]
}";
    }
}
