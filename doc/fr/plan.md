> [English version](../en/plan.md)

# Plan : l'app Kazo Pdf pour Kazo OS

Étape 0 du [guide de portage des apps Craft](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/craft-app-port-guide.md)
de Kazo OS, écrite le 2026-11-13 (issue `TODO` [#1](https://github.com/Kazo-ca/Os-App-PDF/issues/1)).
Ce document fixe le but, ce que la lecture de PdfCraft a montré, les décisions, les questions
posées à Benoit et l'ordre des chantiers. La méthode vient de Kazo Word et de Kazo Spreadsheets
([plan](https://github.com/Kazo-ca/Os-App-Spreadsheets/blob/spreadsheets/base/doc/fr/plan.md)),
la référence la plus proche.

## But

Kazo Pdf est le **lecteur et éditeur PDF de Kazo OS**. Il emballe le build web de
[PdfCraft](https://github.com/storytold/pdfcraft) (équipe ArtCraft, `MIT OR Apache-2.0`), écrit
en Rust (egui), en alpha, et branche son mode hébergé (`craft-web-host`) sur les fichiers, les
dialogues et les associations de Kazo OS. Rien de PdfCraft n'est réécrit.

Le code Kazo commun aux apps Craft (iframe, proxy, sélecteur de fichiers, raccourcis, réglages,
`DocumentSession`, règle d'enregistrement) vient du module `Kazo.Os.AppSdk.Craft` de Kazo OS
([ADR 0259](https://github.com/Kazo-ca/Os/blob/main/doc/fr/wasm/dev/adr/0259-shared-craft-app-host-module.md)),
livré avec **Kazo OS 3.3.3**. Kazo Pdf garde seulement sa fenêtre, sa liste de formats, sa page
d'iframe, le build de PdfCraft et son manifeste.

Aujourd'hui, **aucune app de Kazo OS n'ouvre les `.pdf`** : le navigateur de l'OS montre un PDF
reçu par HTTP, mais aucune app n'est associée à l'extension. Kazo Pdf en devient l'app par défaut.

## Lecture de PdfCraft (étape 0, 2026-11-13)

Lue sur l'étiquette `v0.21.1` d'amont (commit `b503b2e6`, 2026-11-11 ; elle n'est pas sur `main`
du fork [`Kazo-ca/pdfcraft`](https://github.com/Kazo-ca/pdfcraft), comme pour GridCraft) et sur le
zip officiel `pdfcraft-web-0.21.1.zip`.

| Point | Constat |
| --- | --- |
| Build web | `apps/pdfcraft-web` (Trunk, `public_url = "./"`), dépend de `craft-web-host` et `craft-web-api` à la révision `fd5e2fe` de `craft-libs`. Rust 1.95 au minimum. Langues de `craft-i18n` intégrées au `.wasm` (révision `575cfde`). |
| Zip officiel | `https://github.com/storytold/pdfcraft/releases/download/v0.21.1/pdfcraft-web-0.21.1.zip`, SHA-256 `fe9708d46af191aebb56f70eb32cf8b112b0b9452d17ae6ba3a8c4a8665fe748`, **34,4 Mo**. Contenu : `index.html`, colle `pdfcraft-web-<hash>.js` (183 Ko), `pdfcraft-web-<hash>_bg.wasm` (**60,6 Mo**), le **worker OCR** (`pdfcraft-ocr-worker-<hash>.js`, `_bg.wasm` de 3,4 Mo, `_loader.js`), `models/` (deux modèles ONNX, 12,2 Mo, `CC-BY-SA-4.0`), `snippets/`, licences, `HOSTING.md`. 76,6 Mo décompressé, 23 fichiers. |
| Taille | Sous les limites de Kazo OS (128 Mio par fichier ; paquet de 64 Mio compressé, 128 Mio décompressé) : le `.wasm` a la taille de celui de WordCraft. Au-dessus de l'avertissement de GitHub à 50 Mo, sous le refus à 100 Mo (piège connu, déjà accepté pour Kazo Word). |
| Rendu | `eframe::Renderer::Glow` : **WebGL 2 seulement** (le `HOSTING.md` parle de wgpu, mais le code de 0.21.1 ne l'utilise pas). `?webgl` ne change rien ; le parcours WebGPU du guide ne s'applique pas. |
| Canevas | `pdfcraft` (pas `pdfcraft_canvas`), écrit en dur : une instance par document HTML, donc une iframe par fenêtre. |
| Mode hébergé | `data-host="page"` sur le canevas : `CustomEvent` `pdfcraft:*`. `ready` (`file: false` : PdfCraft lit des octets), `open { name, bytes }` (refusé au-delà de 512 Mio ; sans nom : `document.pdf`), `save { name, saveAs, bytes }`, `dirty`, `failed`. `?author=` signe les commentaires. |
| `save` | Envoyé par Enregistrer et Enregistrer sous du document. **`saveAs` est juste** (vrai pour Enregistrer sous, comme WordCraft) : pas de contournement par F12 comme pour GridCraft. Après « Appliquer les caviardages », le nom proposé prend `_Redacted` et `saveAs` devient vrai. Pas d'`id`, pas de confirmation (craft-libs #611). |
| Exports | **Ne passent pas par `save`** : Exporter (Word, images, texte…), Imprimer en PDF, Réduire la taille, copie signée, PDF créé, résultat de l'OCR, données de formulaire (FDF, XFDF, CSV), mesures, capture de zone appellent `download()`, un lien `<a download>` sur un `blob:` : le navigateur **télécharge** le fichier hors de Kazo OS. Question Q1. |
| `dirty` | Vrai si **un** des documents ouverts a des modifications, ou si une relecture OCR est en cours (comme GridCraft). |
| Commandes (`window.pdfcraft`) | Installé seulement avec **`?control`** dans l'adresse de la page. `request`, `execute` (`ui.command { id }`, **sans paramètres**), `commands`, `docInspect` (`ui.state`), `uiInspect`, `screenshot`. Les documents sont dans **`ui.state`** : `documents[{ tab, doc, name, path, pages, dirty, active }]` ; `ui.inspect` est l'arbre d'accessibilité. Thème : `ui.set { key: "theme", value: "dark" }` ; langue : `ui.set { key: "language", value }`. Commandes : `file.save`, `file.save_as`, `file.close`, `file.close_all`, `file.open`… ; pas de `file.new` ni de `file.rename`. |
| Sélecteur du navigateur (`rfd`) | Une vingtaine d'usages : ouvrir (PDF, images, texte), insérer une image ou une page, police TTF/OTF, identité numérique `.p12`/`.pfx`, image de signature, données de formulaire, fichiers à combiner. Tous passent par le même `input type=file` : le sélecteur du module les remplace. |
| Formats | Ouverts : `.pdf` ; images (`png jpg jpeg tif tiff gif bmp jp2 j2k jpx pcx`) et texte (`txt text`) ouverts comme **nouveaux PDF non enregistrés**. Enregistré : `.pdf` seulement. Pas de documents Office sur le web. |
| OCR | « Reconnaître le texte » charge `models/*.onnx` **relatif à la page** et lance des workers de module nommés par `<meta name="pdfcraft-ocr-worker">` dans la page. Notre page d'iframe doit donc porter cette balise et trouver `models/` à côté d'elle. Environ 600 Mo de mémoire par worker (deux au plus). Sans les modèles, tout le reste marche. Question Q2. |
| Réglages | `localStorage` `pdfcraft` (eframe) ; le module les garde par utilisateur Kazo. |
| Fermeture | PdfCraft avertit au `beforeunload` s'il a du travail non enregistré ; la garde de fermeture de la fenêtre passe avant. |
| Langue | 26 langues (`en ja zh-hans zh-hant cs pt-br de es fr ru kk bg te hu uk it ar ckb fa pl sl el tr lt id vi`) ; le chinois n'a pas de police dans la version web (gap 5 d'amont). |
| Taille de fenêtre | Bureau : 1440 × 920, minimum 480 × 520. |
| Marque | Nom et logo ArtCraft : marques. Un build **modifié** les retire ; la fiche du Store dit « basé sur PdfCraft de l'équipe ArtCraft », sans logo. |

## Décisions

| # | Sujet | Décision |
| --- | --- | --- |
| 1 | Nom et identité | « **Kazo Pdf** » (Benoit, 2026-11-13). Id `ca.kazo.pdf`, éditeur `pub.kazo`, assembly `Kazo.Pdf`, catégorie `productivity`, plusieurs fenêtres, fenêtre de 1200 × 820 (le 1440 × 920 du bureau dépasse un écran de portable une fois la barre des tâches comptée), contenu minimum 480 × 520. |
| 2 | Code commun | Module `Kazo.Os.AppSdk.Craft` (ADR 0259). Version minimale : **Kazo OS 3.3.3** (module, `canChangeScreen` de l'ADR 0263, `?autolaunch`), sauf si Q1 demande une version plus récente. |
| 3 | Changement d'écran | `canChangeScreen: false` dans le manifeste dès le chantier 1 (issue [#2](https://github.com/Kazo-ca/Os-App-PDF/issues/2)). |
| 4 | Intégration | Une iframe par fenêtre, page `wwwroot/host/index.html` avec le canevas `pdfcraft` `data-host="page"` `data-craft-app="pdfcraft"`, paramètre `control` pour `window.pdfcraft`. `models/` et le worker OCR doivent se trouver relatifs à la page : la page porte une `<base>` vers `../pdfcraft/` (ou le build est rangé à côté d'elle), et `tools/vendor-pdfcraft.sh` écrit la balise `pdfcraft-ocr-worker` (le nom du worker a un hash). Choisi au chantier 1. |
| 5 | Provenance du build | Comme Kazo Spreadsheets : d'abord le **zip officiel 0.21.1** épinglé par SHA-256 dans `wwwroot/pdfcraft/` avec `pdfcraft.json` ; ensuite compilé depuis le fork `Kazo-ca/pdfcraft` (branche `kazo/v0.21.1`). |
| 6 | Garde de fermeture | Lue dans `ui.state` (`documents[{ doc, name, path, dirty }]`), pas dans `ui.inspect`. La fenêtre la lit elle-même tant que `OpenDocument.ListFrom` du module ne connaît pas cette troisième forme (retour envoyé au module). |
| 7 | Thème et langue | `ui.set { theme }` à chaque changement du thème de l'OS, `ui.set { language }` au démarrage (`CraftLanguage.Pick` sur la liste des 26 codes). |
| 8 | Sélecteur de fichiers | Le module ne dit pas pourquoi PdfCraft demande un fichier (ouvrir, insérer des pages, comparer, image à placer…). Tout fichier choisi est rendu à PdfCraft ; un seul `.pdf` choisi est lié à la fenêtre sous son nom seulement si aucun document ne porte déjà ce nom. |
| 9 | Réponses de Benoit (2026-11-13) | Q1 = **A** (le module intercepte les exports, ADR 0264 dans `Kazo-ca/Os`), Q2 = **A** (modèles OCR dans le paquet), Q3 = **B** (`.pdf` seulement). |

## Questions posées à Benoit

Réponses le 2026-11-13 : Q1 = A, Q2 = A, Q3 = B (décision 9).

**Q1. Les exports partent dans les téléchargements du navigateur.** Exporter, Imprimer en PDF,
Réduire la taille, copie signée, OCR, données de formulaire : PdfCraft les « télécharge » par un
lien `<a download>` au lieu de les remettre à l'hôte. Dans Kazo OS, le fichier arrive dans le
dossier Téléchargements de l'ordinateur réel, pas dans le système de fichiers de Kazo OS.

- **A (recommandée)** : le module `Kazo.Os.AppSdk.Craft` intercepte ces téléchargements dans
  l'iframe (comme il le fait déjà pour le sélecteur de fichiers) et les remet à la fenêtre comme
  une **copie** (dialogue d'enregistrement de l'OS, la liaison ne change pas). Une ADR 0264 et un
  chantier dans `Kazo-ca/Os` ; toute app Craft en profite. Kazo Pdf demande alors la version de
  l'OS qui l'apporte (après 3.3.3), donc sa sortie au Store attend cette version. En parallèle,
  Claude prépare l'issue amont (envoyer les exports par `save` en mode hébergé), que Benoit
  publie.
- B : corriger seulement en amont (issue et PR préparées par Claude, publiées par Benoit) ;
  Kazo Pdf sort sur 3.3.3 avec la limite écrite dans la fiche jusqu'à la version de PdfCraft qui
  la corrige.
- C : corriger dans notre fork (chantier 2), donc un build modifié dès la v1 (retrait des marques
  ArtCraft).

**Q2. Les modèles OCR (12,2 Mo, `CC-BY-SA-4.0`).**

- **A (recommandée)** : les livrer dans le paquet (34 Mo compressé au lieu d'environ 22) ; la
  reconnaissance de texte marche hors ligne ; licence et attribution dans
  `THIRD-PARTY-NOTICES.md` (licence de la liste permise).
- B : les retirer ; « Reconnaître le texte » dit que le téléchargement a échoué (limite écrite
  dans la fiche).

**Q3. Formats associés à Kazo Pdf.**

- **A (recommandée)** : app par défaut pour `.pdf` ; « Ouvrir avec » pour les images
  (`.png .jpg .jpeg .tif .tiff .gif .bmp .jp2 .j2k .jpx .pcx`) et `.txt`, qui s'ouvrent comme un
  nouveau PDF (les visionneuses et l'éditeur de l'OS restent par défaut).
- B : `.pdf` seulement.

## Chantiers

Série `pdf/base`, une branche `pdf/<n>-<sujet>` et une PR vers `pdf/base` par chantier. Benoit
fusionne `pdf/base` dans `main`. Le module fait une grande part des chantiers 1 à 3 du guide, et
Kazo Spreadsheets a déjà écrit une fenêtre complète sur lui : la solution et la fenêtre forment ici
un seul chantier.

| N° | Branche | Contenu | Dépend de |
| --- | --- | --- | --- |
| 0 | `pdf/0-plan` | ce plan (fr et en) | — |
| 1 | `pdf/1-solution` | solution renommée (`Kazo.Pdf`, `.Os`, `.Tests`), manifeste (`canChangeScreen: false`, OS 3.3.3, `.pdf` seulement), `tools/vendor-pdfcraft.sh` (zip 0.21.1 épinglé, modèles selon Q2, balise du worker) et `pdfcraft.json`, page d'iframe ; fenêtre sur le module : ouvrir, enregistrer, Enregistrer sous, garde de fermeture (`ui.state`), sélecteur, raccourcis, thème, langue, auteur, réglages ; deux fenêtres ; tests xUnit et parcours Playwright ; sous-module de l'OS sur `main` (3.3.3) jusqu'à l'étiquette `v3.3.3` | plan fusionné |
| 2 | `pdf/2-build-from-source` | fork `Kazo-ca/pdfcraft` en sous-module (branche `kazo/v0.21.1`), `tools/build-pdfcraft.sh` (`craft-i18n` épinglé, `cargo xtask models`), workflow `build-pdfcraft.yml`, test du commit | chantier 1 |
| 3 | `pdf/3-exports` | selon Q1 : la fenêtre reçoit les exports comme copies (A, après le chantier de l'OS), ou rien ici (B) | chantier 1, réponse à Q1 |
| 4 | `pdf/4-store` | icône sans logo ArtCraft, fiche fr et en, README refait sur la fiche (`store:readme`, « Essayer » avec `?autolaunch=ca.kazo.pdf`), `THIRD-PARTY-NOTICES.md`, installation dans un Kazo OS publié, tag par « Tag a release », catalogue (signature dans `Os-AppMarketPlace`), `features.md` | chantiers 1 à 3, version de l'OS demandée |

**Terminé quand** : Kazo Pdf s'installe depuis le Store d'un Kazo OS publié, ouvre un `.pdf` par
double-clic, l'enregistre, et deux fenêtres tournent ensemble.

## Piste en amont

Textes préparés par Claude en anglais, publiés par Benoit (pas de mention d'agent) :

- PdfCraft : en mode hébergé, remettre les exports à l'hôte par `save` (Q1) ;
- PdfCraft : `HOSTING.md` parle de wgpu et de `?webgl`/`?cpu`, alors que le build web utilise Glow ;
- `craft-libs` : se joindre à #611 (confirmation d'enregistrement) et #610 (`open-request`).

## Avancement

- [x] Étape 0 : lecture de PdfCraft 0.21.1 et du zip officiel.
- [x] Plan fr et en.
- [x] Réponses de Benoit à Q1, Q2 et Q3 (A, A, B).
- [x] Chantier 1 : solution et fenêtre (`pdf/1-solution`).
- [x] Chantier 2 : build depuis le fork `Kazo-ca/pdfcraft` (`pdf/2-build-from-source`).
- [ ] Chantiers 3 et 4.
