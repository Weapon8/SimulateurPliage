# Cotes à mesurer — Simulateur de pliage (Loiresafe 4 m)

Toutes ces valeurs sont **ajustables dans l'app** (panneau « RÉGLAGES MACHINE (À MESURER) »).
Le proto tourne déjà avec des valeurs approximatives — remplace-les par les vraies pour que
les collisions soient justes. Prends-les au pied à coulisse / mètre, poinçon et matrice **de côté**.

## Poinçon (vu de côté / en coupe)
- [ ] **Hauteur totale** du poinçon (déjà : ~120 mm) — vérifier.
- [ ] **Angle de pointe** (déjà : 35°) — vérifier.
- [ ] **Largeur en pointe** (mm) : l'épaisseur du bec tout en bas (souvent 0,5–2 mm).
- [ ] **Col de cygne — retrait** : de combien la face du poinçon **rentre** (recul horizontal, mm)
      quand on monte vers le corps. C'est LE dégagement qui laisse passer un retour de bord.
- [ ] **Col de cygne — hauteur** : à quelle **hauteur au-dessus de la pointe** ce creux commence (mm).

  → Le plus simple : photo du poinçon de côté avec un réglet posé dessus, je relève retrait + hauteur.

## Matrice (vue de côté)
- [ ] **Ouvertures V** dispo (déjà : 12 / 16 / 24) — confirmer la liste complète.
- [ ] **Largeur du bloc** (épaulement) pour chaque V : largeur totale du bloc matrice de part
      et d'autre du V (le proto suppose bloc ≈ 2 × V — à corriger).
- [ ] **Angle du V** (déjà : 45°) — vérifier.
- [ ] Profondeur du V (indicatif, mm).

## Plieuse
- [ ] **Hauteur libre ouverte** (déjà : ~120 mm) : garde verticale poinçon↔matrice, tablier ouvert.
- [ ] **Course butée arrière** max (déjà : 700 mm).
- [ ] **Tablier — déport bas** (déjà : 50 mm).
- [ ] Col de cygne latéral : passage 3100 / profondeur 500 (info, pour grandes boîtes — pas la section).

## Notes proto
- Calcul de pli : **cotes intérieures / extérieures** seulement (pas encore d'allongement / fibre neutre).
- Modèle de collision : **section 2D**, par étape, sur le volet en formage vs poinçon (col de cygne),
  hauteur libre, butée, et repli-sur-repli. Le vrai 3D et l'import depuis les modules viendront après.
