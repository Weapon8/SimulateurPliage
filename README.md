# Simulateur de pliage — collisions outillage

Prototype **WinForms .NET 8** (thème TolTem) pour visualiser une **séquence de pliage** sur plieuse
et **détecter les collisions** pièce ↔ outillage (col de cygne, hauteur libre, butée, repli-sur-repli),
en **section 2D**, étape par étape.

> Fait pour la plieuse **Loiresafe 4 m**. Toutes les cotes machine sont **ajustables dans l'app**.
> C'est un **proto** : les cotes de départ sont approximatives (voir `COTES_A_MESURER.md`).

---

## Ce que fait le proto

- Saisie **manuelle** d'une pièce : nombre de plis, épaisseur, longueurs des pans.
- **Cotes intérieures ou extérieures** (proto sans allongement / fibre neutre pour l'instant).
- **Séquence de pliage** ordonnée : par étape → Pli, Angle°, Sens (Haut/Bas), V (12/16/24), **Reprise**.
- **Timeline multi-passes** : une même ligne peut être **marquée** (ex. 130°) puis **fermée** plus tard
  (ex. 90°) — coche « Reprise » sur les passes concernées.
- **Vue 2D de section** : matrice (V), poinçon **avec son creux de col de cygne**, hauteur libre,
  et la pièce repliée à l'étape courante.
- **Lecture pas-à-pas** : boutons ◀ ▶ et slider ; l'angle `départ→cible` est affiché.
- **Code couleur** (vue + énumération) :
  - **bleu** = pli direct / définitif,
  - **vert** = pli avec reprise (marquage puis fermeture),
  - **rouge** = collision détectée.
- **Détections** : col de cygne, hauteur libre ouverte, butée arrière (course max), repli-sur-repli,
  avec message « dépasse de X mm, dispo Y ».
- **Énumération** de toute la séquence en bas (n°, pli, angles, sens, V, cote butée, état).

---

## Réglages machine (À MESURER)

Panneau à gauche, section « RÉGLAGES MACHINE ». Valeurs de départ (à corriger) :

| Cote | Défaut proto |
|---|---|
| Poinçon — hauteur | 120 mm |
| Poinçon — pointe | 35° |
| Poinçon — largeur pointe | 2 mm *(à mesurer)* |
| Col de cygne — retrait | 40 mm *(à mesurer)* |
| Col de cygne — hauteur | 25 mm *(à mesurer)* |
| Matrices V | 12 / 16 / 24 |
| Épaulement matrice | ≈ 2 × V *(à mesurer)* |
| Tablier — déport | 50 mm |
| Hauteur libre ouverte | 120 mm |
| Butée arrière max | 700 mm |

Détail et méthode de mesure : voir **`COTES_A_MESURER.md`**.

---

## Build

Prérequis : **SDK .NET 8**.

- Double-clic sur **`build.bat`** → publie un **exe autonome** dans `exe_final\SimulateurPliage.exe`
  (single-file, self-contained win-x64).
- Le `.bat` ferme l'appli si elle tourne (évite le « l'exe saute »), nettoie `exe_final`, vérifie l'exe.

Ou en ligne de commande :

```
dotnet publish SimulateurPliage.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o exe_final
```

---

## Fichiers

| Fichier | Rôle |
|---|---|
| `Program.cs` | point d'entrée |
| `Model.cs` | `MachineConfig` (cotes), `Piece`, `Operation`, enums |
| `FoldEngine.cs` | repliage 2D par étape + détection de collision |
| `SectionPanel.cs` | dessin de la section (matrice, poinçon, pièce, collisions) |
| `MainForm.cs` | UI : tables pans/séquence, réglages, vue, énumération |
| `COTES_A_MESURER.md` | checklist des cotes à relever |

---

## Suite (paliers)

1. **Import** depuis DeveloppeProfil / DeveloppeCheneau (récupérer segments + plis).
2. **Ordre de pliage auto** (tester les permutations sans collision).
3. **Allongement / fibre neutre** (cotes précises débit).
4. **Vue 3D décomposée** du pliage.

Made with ♥ by weapon666 pour TolTem.
