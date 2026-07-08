# SimulateurPliage — intégration des fichiers

## À faire dans le projet `SimulateurPliage`

### 1. Supprimer
- `Poincon.patch.cs` (si présent) — c'était un mémo, pas un fichier à compiler.

### 2. Remplacer (écraser l'ancien du même nom)
- `Tools.cs` — poinçon P.120.35.R3 : contour exact (bec 10°/25°, pointe R3, col de cygne, corps 26, queue 18) + **hauteur réglable** (le fût s'étire, bec/col figés).
- `FoldEngine.cs` — moteur pliage + collision : l'âme du poinçon est exclue (point-dans-contour), plus de faux positif.
- `SectionPanel.cs` — vue Section : tôle en trait franc lisible + dessin du **contour réel** du poinçon.
- `MainForm.cs` — UI : séparateurs entre sections, réglages machine groupés, grille SÉQUENCE agrandie, bouton **Développé**.

### 3. Ajouter (nouveau fichier)
- `DeveloppePanel.cs` — vue **Développé** : tôle à plat, lignes de pli, face bleue / rouge quand il faut retourner la pièce.

### 4. Rebuild propre
- Supprimer les dossiers `bin\` et `obj\`.
- Republier win-x64 (single-file, self-contained).
- La version 11 régénère automatiquement `outils.json`.

## À jeter
- Tous les fichiers `.png` (comparaisons, contour, hauteurs, etc.) — ils servaient juste à valider la forme à l'écran, aucun rôle dans le projet.

## État du projet
- ✅ Poinçon exact + hauteur réglable
- ✅ Collision (âme exclue)
- ✅ Vue Section (trait franc + contour réel)
- ✅ Vue Développé (retournement bleu/rouge)
- ✅ UI (séparateurs + séquence agrandie)

## Reste à faire (quand tu veux)
- Matrice (V) en SVG, même méthode que le poinçon.
- Cotes bâti Loire Safe 4 m / Amada 2 m → presets machine (outillage partagé).
- Profil poinçon au centième : déjà exact via le SVG ; sinon DXF Rolleri le jour où tu l'as.
