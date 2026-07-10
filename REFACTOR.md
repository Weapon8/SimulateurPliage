# Refactor — nouvelle architecture

## Migration (à faire une fois)

### 1. Supprimer du projet
Tous les anciens fichiers `.cs` **sauf** `SimulateurPliage.csproj` et `toltem.ico` :
`Model.cs`, `Tools.cs`, `FoldEngine.cs`, `MainForm.cs`, `SectionPanel.cs`,
`DeveloppePanel.cs`, `PupitrePanel.cs`, `Program.cs`.

### 2. Copier la nouvelle arborescence
```
SimulateurPliage/
  Program.cs
  Materiel/     Plieuse.cs  Poincon.cs  Matrice.cs  Atelier.cs
  Pliage/       Piece.cs  Moteur.cs  Detecteur.cs
  Vues/         Theme.cs  VueSection.cs  VueDeveloppe.cs  VuePupitre.cs  FenetrePrincipale.cs
```
Le `.csproj` est en SDK-style : il compile les sous-dossiers tout seul, rien à modifier.

### 3. Rebuild
Vider `bin/` et `obj/`, puis `build.bat`.
La bibliothèque est régénérée dans `atelier.json` (nouveau nom, l'ancien `outils.json` est ignoré).

---

## Les trois couches

**`Materiel/`** — le matériel physique. Ne connaît pas le pliage.
- `Plieuse.cs` : cotes bâti, butée, hauteur libre. **Presets Loire Safe 4 m et Amada 2 m.**
- `Poincon.cs` : contour figé, hauteur réglable (le fût s'étire, bec et col de cygne intacts).
- `Matrice.cs` : bloc, vés, embases.
- `Atelier.cs` : la bibliothèque, persistée en JSON.

**`Pliage/`** — le calcul. **Aucune dépendance WinForms** : testable seul, portable en JS.
- `Piece.cs` : pans, opérations, séquence, retournements.
- `Moteur.cs` : chaîne de la fibre neutre, ancrage sur le pli actif.
- `Detecteur.cs` : collisions contre l'outillage.

**`Vues/`** — l'affichage seul. Dessine, ne calcule pas.
- `Theme.cs` : palette TolTem centralisée (une seule source).
- `VueSection.cs`, `VueDeveloppe.cs`, `VuePupitre.cs`, `FenetrePrincipale.cs`.

---

## Renommages

| Avant | Après |
|---|---|
| `MachineConfig` | `Materiel.Plieuse` |
| `ToolLib` | `Materiel.Atelier` |
| `FoldEngine.Build()` | `Pliage.Moteur.Construire()` |
| `StepState` | `Pliage.EtatEtape` |
| (dans FoldEngine) | `Pliage.Detecteur` |
| `MainForm` | `Vues.FenetrePrincipale` |
| `SectionPanel` | `Vues.VueSection` |
| `DeveloppePanel` | `Vues.VueDeveloppe` |
| `PupitrePanel` | `Vues.VuePupitre` |

## Corrigé au passage
- **`+ étape` remarche** même après avoir supprimé toutes les opérations : il crée la ligne de pli manquante.
- **`+ pli`** ajoute vraiment un pan et son opération.
- Le champ **Hauteur poinçon** pilote le contour en direct.
- Sélecteur **Plieuse** ajouté (Loire Safe / Amada).

## Supprimé (champs morts)
`ColRetrait`, `ColHauteur`, `BecHauteur`, `EpaulementFactor`, `PoinconFaceX`,
les palettes de couleurs dupliquées dans chaque panneau.

---

## À relever sur machine
- `Plieuse.TonnageMax` et `Plieuse.DoigtHauteur` (les deux à 0).
- Toutes les cotes de l'**Amada 2 m** (actuellement estimées, marquées `TODO`).
