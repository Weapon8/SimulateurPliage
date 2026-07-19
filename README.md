# Simulateur de pliage — collisions outillage

**WinForms .NET 8** (thème TolTem). Visualise une **séquence de pliage** en section 2D et en 3D,
étape par étape, **détecte les collisions** pièce ↔ outillage — et **trouve l'ordre de pliage
tout seul**.

Fait pour la plieuse **Loire Safe 4 m**, poinçon **Rolleri P.150.35.R2**, matrices Euram.
Toutes les cotes machine et outillage sont ajustables dans l'app.

> Le but n'est pas de remplacer le plieur. C'est de donner à quelqu'un qui débute l'ordre de
> pliage qu'un plieur expérimenté trouve d'instinct — et de le prévenir quand il va avoir les
> doigts près du bec, ou quand une aile va taper le tablier.

## But final du projet

Le cap, c'est la **saisie zéro** : **scanner un croquis coté** (jamais une photo — un dessin coté
à la main ou un plan) et en **déduire automatiquement** les plis, les angles, les **faces** et le
**développé à plat** — puis lancer le solveur dessus. Aujourd'hui l'opérateur saisit pans + angles
+ faces à la main ; demain il scanne son croquis, l'outil lit la géométrie, et sort l'ordre de
pliage.

---

# 🚀 Prise en main rapide

## Le vocabulaire en 30 secondes

- **Pan** = un côté droit de la tôle (un segment). Une pièce à 5 plis a 6 pans.
- **Pli** = la ligne où ça plie, entre deux pans. Numéroté par **`Bend`** (0 = entre pan 0 et pan 1).
- **Angle** = de combien on plie **depuis le plat**. **180 = plat, 90 = équerre, 45 = aigu.**
  (Ce n'est PAS l'angle intérieur géométrique.)
- **Face** = de quel côté est le laquage. **FNL** = non laqué (bleu), **FL** = laqué/brillant
  (violet). C'est une **donnée de la pièce**, lue sur le dessin — elle ne change pas.
- **⇄ (`ButeeAval`)** = on retourne la tôle **à plat**, bout pour bout.
- **⇅ (`Retournee`)** = on retourne la tôle **dessus/dessous**.

## Le mini-tuto : plier une équerre puis un chéneau

### 1) Une simple équerre (2 pans, 1 pli)

1. Ouvre l'app. Dans **PIÈCE**, mets `Nombre de plis = 1`.
2. Dans la table **PLIS**, tu as une ligne. Renseigne `Longueur`, `Angle = 90`, `Face = FNL`.
3. Regarde la **vue Section** : la tôle est posée sur la matrice, le poinçon descend, l'aile monte.
4. Bandeau en bas : **« Pas de collision »** (vert). L'équerre est pliable telle quelle.

### 2) Charger une pièce de référence

1. Dans **PROFILS → Bibliothèque**, déroule et choisis **« Références · Chéneau … »**, clique
   **Charger**.
2. La table PLIS se remplit : 5 plis, du 45° (raidisseur) au 90°.
3. Fais glisser le **curseur d'étapes** en haut : tu vois la tôle se former pli par pli.
4. À l'**étape 5**, le grand volet (le 200) vient **buter sur le pli du 100** déjà formé — c'est
   la 2ᵉ butée. La couleur passe **bleue** (FNL dessus).

### 3) Laisser l'outil trouver l'ordre

1. Sur n'importe quelle pièce, clique **« Ordre auto »**.
2. Le solveur essaie toutes les combinaisons, garde celles qui ne tapent rien, et classe :
   **pli fermé en premier → moins de retournements → plus grande prise opérateur**.
3. La séquence se réordonne. Le **pupitre** (bas) montre la gamme façon CN.

### 4) Lire les alertes

- **Main rouge** (haut droite de la section) : il reste peu à tenir côté opérateur ET un pli
  franchement aigu est en jeu → attention aux doigts.
- **Trait rouge** sur la tôle : collision — l'aile ou le retour tape l'outillage à cette étape.
- **Vert** : reprise (pli en plusieurs passes).

---

# 📐 Syntaxe d'une pièce (JSON)

Une pièce est décrite par ses **pans**, ses **angles**, ses **faces**, et sa **séquence** de
pliage. Voici le **chéneau** de référence, commenté :

```jsonc
{
  "Nom": "Chéneau 30·40·150·200·100·10",
  "Epaisseur": 1.0,
  "LongueurPli": 500,          // profondeur de la tôle (mm), sens de l'axe de pli
  "Rm": 450,                    // N/mm² : acier 450, inox 600, alu 250, zinc 150
  "CotesExterieures": false,    // false = cotes intérieures

  "Segments": [30, 40, 150, 200, 100, 10],   // les 6 PANS, dans l'ordre du profil
  "Angles":   [90, 90, 90, 90, 45],          // angle de chaque PLI (5 plis = 5 angles)
  "Faces":    [false, true, false, false, false],  // false=FNL, true=FL — par pli
  "FacesManuelles": true,       // true = les faces sont saisies/lues, elles font foi

  "Sequence": [                 // l'ORDRE de pliage (une entrée = une étape)
    { "Bend": 4, "AngleCible": 45, "V": 16 },                   // 1) le raidisseur (pli du 10)
    { "Bend": 3, "AngleCible": 90, "V": 16 },                   // 2) le pli du 100
    { "Bend": 0, "AngleCible": 90, "V": 16, "ButeeAval": true },// 3) le 30, retourné à plat ⇄
    { "Bend": 1, "AngleCible": 90, "V": 16, "Retournee": true },// 4) le 40, retourné ⇅
    { "Bend": 2, "AngleCible": 90, "V": 16, "Retournee": true } // 5) le 200, retourné ⇅
  ]
}
```

**Les règles de cohérence à respecter :**

| Champ | Règle |
|---|---|
| `Segments` | N+1 pans pour N plis. Ordre = comme on parcourt le profil. |
| `Angles` | Un par pli, **dans l'ordre des pans** (angle du pli qui suit le pan i). |
| `Faces` | Un par pli, **dans l'ordre des pans**. `false`=FNL (bleu), `true`=FL (violet). |
| `Sequence[].Bend` | L'index du pli (0 = 1ᵉʳ pli). C'est **l'ordre de pliage**, ≠ ordre des pans. |
| `AngleCible` | 180=plat, 90=équerre, 45=aigu. **Angle de pli**, pas l'angle intérieur. |
| `ButeeAval` (⇄) | retournement **à plat**. La face ne change pas ; la butée lit le pan aval. |
| `Retournee` (⇅) | retournement **dessus/dessous**. |
| `V` | ouverture du vé de la matrice (mm). |

> ⚠️ **Le piège classique** : `Faces` et `Angles` sont indexés **par pan** (ordre du profil),
> mais `Sequence[].Bend` est **l'ordre de pliage**. Le chéneau se plie 4,3,0,1,2 mais ses faces
> se lisent 0,1,2,3,4.

## Les pièces de référence sont figées — ne pas les recoder

Les pièces validées à l'atelier vivent dans **`Pliage/ProduitReference.cs`** (une constante JSON,
**sortie du code en dur**). La bibliothèque les lit au démarrage. **On ne retouche plus le moteur
pour une pièce de référence** ; si l'une doit changer, on régénère ce JSON à part.

---

## LA règle de fond : la FACE commande tout

> **La FACE d'un pli (donnée de la pièce, lue sur le dessin, fixe) détermine la cote de butée, le
> sens du dessin, l'orientation de la tôle et la couleur. Elle ne se confond PAS avec le
> retournement ⇄/⇅, qui est un geste de la séquence.**

Ce qui en découle, et qui est codé ainsi :

1. **Cote de butée** — pli intérieur (FNL) cale sur le pan **AVAL** ; pli extérieur (FL) cale sur
   le pan **AMONT** ; ⇄ inverse. Donne la gamme du chéneau **10 · 100 · 30 · 40 · 200**.
2. **Sens du dessin** — un pli suit le sens de sa face (même face que l'actif → même sens, opposée
   → inverse). Donne la **forme** correcte (le U du chéneau) au lieu d'un zigzag.
3. **Orientation** — le pan **de cote** (calé contre la butée) part à **DROITE**, le **grand corps**
   à **GAUCHE** (opérateur). Grand pan à gauche en direct, à droite avec retournement/appui.
4. **Couleur** — suit la **face déclarée du pli en cours** : `false`=FNL=bleu, `true`=FL=violet.

---

## Les règles figées (contrôlées par l'autotest)

1. **Orientation** — pan de cote à droite, grand corps à gauche (opérateur).
2. **Sommet** — le sommet du pli actif est à l'origine, sous la pointe du poinçon.
3. **Tous les plis vont vers le HAUT.** Les changements de direction viennent des faces et
   retournements, jamais d'un pli vers le bas.
4. **Le contour du poinçon est figé** (relevé vectoriel). Seule `Hauteur` est réglable.
5. **Prise opérateur** — toujours le plus grand côté vers l'opérateur quand c'est possible.
6. **Plancher d'angle** — on ne ferme pas plus que l'angle du vé.
7. **Pli fermé en premier** — le pli le plus aigu est le 1er geste (contrainte dure sur le 1er
   pli seulement).

---

## Vocabulaire machine (à ne pas confondre)

- **Tablier** = le **HAUT mobile** : coulisseau + « inter » (support poinçon) + poinçon.
- **Bâti + support matrice** = le **BAS fixe** = référence y = 0 sur la face matrice.
- **« Inter »** = support poinçon = `porte-poinçon` dans le code.
- **Raidisseur** = un pli à 45°. **Pli écrasé** = formé autrement (Jouanel), **hors périmètre**.

---

## Architecture — trois couches

```
Materiel/   Plieuse.cs  Poincon.cs  Matrice.cs  Atelier.cs     le matériel physique
Pliage/     Piece.cs  Moteur.cs  Detecteur.cs  Solveur.cs      le calcul — zéro WinForms
            Autotest.cs  PieceIO.cs  Bibliotheque.cs
            ProduitReference.cs   (les pièces de référence figées en JSON)
Vues/       Theme.cs  VueSection.cs  VueVolume.cs               l'affichage seul
            VueDeveloppe.cs  VuePupitre.cs  FenetrePrincipale.cs
```

Dépendance à sens unique : **Vues → Pliage → Materiel**.

`Pliage/` et `Materiel/` n'ont **aucune dépendance UI** : ils compilent en console sur n'importe
quelle plateforme. C'est ce qui permet de faire tourner le moteur et l'autotest sans Windows —
et ce qui les rendra portables en JS pour le hub TOLTEM, et branchables sous le futur module de
scan de croquis.

---

## Outillage

**Poinçon — Rolleri P.150.35.R2** — Hauteur 150 (utile 120), bec 35° (10°/25°), R2, col de cygne.
Contour figé, pointe à (0,0). Flanc droit (butée) étroit, flanc gauche (opérateur) large.

**Matrices Euram** — profil en **T inversé (⊥)** : tête étroite (vé), pied large.

| Preset | Tête | Pied | H | Vés |
|---|---|---|---|---|
| 2035 / 35° | 26 | 60 | 80 | V8, V12 |
| 2045 / 45° | 30 | 60 | 80 | V10, V12, V16, V20, V25 |
| Euram 2009 · 4 voies | 60×60 | | 60 | V50·85°, V35·85°, V22·88°, V16·88° — R2 |

**Plieuse — Loire Safe 4 m**

| Cote | Valeur | Utilisée par |
|---|---|---|
| Butée arrière — profondeur | 10,2 → 695 mm | butée mini (Detecteur) |
| Longueur de pli admissible | 100 → 4050 mm | contrôle longueur (Detecteur) |
| **Tablier — garde bec→tablier** | **280 mm** | **collision tablier (Detecteur)** |
| **Tablier — débord latéral** | **66 mm / côté** | collision tablier |

**Collision tablier** : une aile qui monte au-delà de 280 mm tape le haut. Hauteur d'aile =
**vraie hauteur verticale** `L·sin(180−angle)`, pas la projection bissectrice. Marge flexion 5 mm.
**Aile mini** = `max(0,63 × V, butée mini)`, testée sur les deux pans. Butée mini tolère 0,5 mm.

Cotes encore à relever : voir **ROADMAP.md**.

---

## Build

Prérequis : **SDK .NET 8**.

Double-clic sur **`build.bat`** → exe autonome dans `exe_final\SimulateurPliage.exe`.

Ou :

```
dotnet publish SimulateurPliage.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o exe_final
```

**Le cœur se compile sans Windows.** `Pliage/` + `Materiel/` dans un projet console `net8.0`, et
`Autotest.Executer(...)` tourne sur Linux comme sur Mac. Indispensable pour vérifier une modif de
géométrie **au banc** avant de la pousser.

---

Suite : voir **ROADMAP.md**.

Made with ♥ by weapon666 pour TolTem.
