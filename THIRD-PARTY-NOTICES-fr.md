# Avis de tiers

> [English version](THIRD-PARTY-NOTICES.md)

Kazo Pdf est sous licence MIT ([LICENSE](LICENSE)). Son code C#, sa page d'iframe
(`src/Kazo.Pdf/wwwroot/host/`) et son icône (`src/Kazo.Pdf/wwwroot/icons/app.svg`,
`store/icon.png`) sont l'œuvre de ce dépôt. Le proxy d'iframe qu'il utilise (`craft-proxy.js`)
appartient à Kazo OS (`Kazo.Os.AppSdk.Craft`, MIT).

Il embarque une œuvre de tiers, **PdfCraft**, qui apporte ses propres éléments de tiers, dont les
modèles de reconnaissance de texte.

## PdfCraft

| | |
| --- | --- |
| Œuvre | [PdfCraft](https://github.com/storytold/pdfcraft), un éditeur PDF écrit en Rust, de l'équipe ArtCraft et des contributeurs de PdfCraft |
| Version | 0.21.1, compilée depuis notre fork [`Kazo-ca/pdfcraft`](https://github.com/Kazo-ca/pdfcraft) (branche `kazo/v0.21.1`, l'étiquette amont, sans modification) |
| Licence | MIT ou Apache-2.0, au choix de l'utilisateur |
| Fichiers | `src/Kazo.Pdf/wwwroot/pdfcraft/` (le build web : `.js`, `.wasm`, `snippets/`, le worker OCR, `models/`) |
| Provenance | `src/Kazo.Pdf/wwwroot/pdfcraft/pdfcraft.json` : le commit du fork, les compilateurs et le SHA-256 de chaque fichier |

Kazo Pdf ne modifie pas PdfCraft : il montre son build web dans un iframe et lui parle par les
commandes de contrôle et les événements que PdfCraft publie pour une page hôte.

Les avis de PdfCraft sont livrés **sans modification** à côté de son build, et dans le paquet
installé (`_content/Kazo.Pdf/pdfcraft/`) :

| Fichier | Contenu |
| --- | --- |
| `LICENSE-MIT`, `LICENSE-APACHE` | Les licences de PdfCraft |
| `NOTICE` | Le copyright de PdfCraft, son avis de marque, et les éléments de tiers qu'il embarque, avec les avis que leurs licences demandent |
| `models/ATTRIBUTION.txt`, `models/*.LICENCE.txt` | Les modèles de reconnaissance de texte et leur licence (plus bas) |

### Fichiers que le NOTICE cite

Le `NOTICE` de PdfCraft nomme des fichiers de licence de son dépôt source que sa version web ne
contient pas. Kazo Pdf les livre, copiés sans modification depuis le fork au commit embarqué
(`b503b2e62dfeec5598f344e4e287d9c9cbdb34fa`), dans `src/Kazo.Pdf/wwwroot/legal/pdfcraft/` :

| Fichier | Source dans PdfCraft | Couvre |
| --- | --- | --- |
| `ATTRIBUTION.md` | `ATTRIBUTION.md` | La liste par fichier des ressources de PdfCraft (auteur, source, licence) |
| `OFL-Inter.txt`, `OFL-JetBrainsMono.txt`, `OFL-DancingScript.txt` | `assets/fonts/` | Les polices Inter, JetBrains Mono et Dancing Script (SIL Open Font License 1.1) |
| `LICENSE-lucide.txt` | `assets/icons/` | Les icônes Lucide (ISC et MIT) |
| `egui-OFL.txt`, `egui-UFL.txt`, `egui-Hack-Regular.txt`, `egui-emoji-icon-font-mit-license.txt` | `licenses/` | Les polices par défaut d'egui : Noto Emoji (OFL 1.1), Ubuntu Light (Ubuntu Font Licence 1.0), Hack (MIT et Bitstream Vera), emoji-icon-font (MIT) |
| `ICU4X-LICENSE.txt` | `licenses/` | Les données de coupure de ligne d'ICU4X (Unicode-3.0) |
| `CMap-LICENSE.txt` | `licenses/` | Les ressources CMap embarquées par hayro-cmap (BSD-3-Clause) |
| `prc-rs-LICENSE.txt` | `licenses/` | prc-rs |
| `LICENSE_FOXIT` | `vendor/hayro-interpret/assets/` | Les polices de substitution standard-14 de Foxit (BSD-3-Clause) |
| `edit-session-LICENSE-MIT`, `edit-session-LICENSE-APACHE` | `vendor/edit-session/` | edit-session |

La version web de PdfCraft n'embarque aucune police de craft-fonts.

### Modèles de reconnaissance de texte

| | |
| --- | --- |
| Œuvre | Les modèles Ocrs de détection et de reconnaissance de texte (`text-detection.onnx`, `text-recognition.onnx`), de Robert Knight, entraînés sur HierText |
| Source | [ocrs](https://github.com/robertknight/ocrs) (modèles ONNX publiés par l'auteur), téléchargés et vérifiés par SHA-256 par `cargo xtask models` de PdfCraft |
| Licence | [CC-BY-SA-4.0](https://creativecommons.org/licenses/by-sa/4.0/legalcode), texte complet dans `models/*.LICENCE.txt` |
| Modifications | Aucune : les modèles sont livrés sans modification |

### La marque ArtCraft

Le nom, le logotype et les logos ArtCraft sont des marques de l'équipe ArtCraft ; la licence libre
de PdfCraft ne les couvre pas. Kazo Pdf n'en utilise aucun : son nom, son icône et sa fiche du
Store sont l'œuvre de ce dépôt. « Basé sur PdfCraft de l'équipe ArtCraft » dit seulement d'où vient
le logiciel.

## Dans le paquet

Le paquet Kazo OS (`.kazoapp`) contient, sous `wwwroot/` :

- `legal/LICENSE.txt` : la licence MIT de Kazo Pdf (le `LICENSE` de ce dépôt) ;
- `legal/THIRD-PARTY-NOTICES.md` et `legal/THIRD-PARTY-NOTICES-fr.md` : ce fichier et sa version
  anglaise ;
- `legal/pdfcraft/` : les fichiers de licence que cite le `NOTICE` de PdfCraft, comme plus haut ;
- `pdfcraft/LICENSE-MIT`, `LICENSE-APACHE`, `NOTICE`, `models/ATTRIBUTION.txt` et
  `models/*.LICENCE.txt` : les avis de PdfCraft et la licence des modèles, comme plus haut.

Un test C# (`LegalFilesTests`) vérifie que le projet met ces fichiers dans le paquet.

## Données de test

Les tests de bout en bout fabriquent leurs propres documents avec Kazo Pdf ; ils ne livrent aucun
fichier de tiers.
