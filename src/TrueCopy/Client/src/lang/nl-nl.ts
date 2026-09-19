/**
 * Language Alias: nl-nl
 * Language Int Name: Dutch (NL)
 * Language Local Name: Nederlands (NL)
 * Language Culture: nl-NL
 *
 * Typed as `Translation<TrueCopyLocalizations>`, so leaving a key out of this file is a build
 * error rather than a silent fall back to English.
 */
import type { UmbLocalizationDictionary } from "@umbraco-cms/backoffice/localization-api";
import type { TrueCopyLocalizations } from "./en.js";
import type { Translation } from "./types.js";

const nl: Translation<TrueCopyLocalizations> = {
  trueCopy: {
    // Product name, deliberately untranslated.
    actionLabel: "True Copy…",

    resultHeadline: (name: string) => (name ? `${name} gekopieerd` : "Gekopieerd"),
    done: "Klaar",

    pagesCopied: (count: number) => (count === 1 ? "pagina gekopieerd" : "pagina's gekopieerd"),
    linksRepointed: (count: number) =>
      count === 1 ? "link omgezet naar de kopieën" : "links omgezet naar de kopieën",
    copiedPagesChanged: (count: number) =>
      count === 1 ? "gekopieerde pagina gewijzigd" : "gekopieerde pagina's gewijzigd",

    noExternalReferences: "Niets in de kopieën verwijst nog naar de originele pagina's.",
    externalReferences: (count: number) =>
      count === 1
        ? "1 link verwijst naar een pagina die geen deel uitmaakte van de kopie. Die blijft ongewijzigd — hij klopt nog steeds — maar misschien wil je dat de kopieën ergens anders naartoe verwijzen."
        : `${count} links verwijzen naar pagina's die geen deel uitmaakten van de kopie. Die blijven ongewijzigd — ze kloppen nog steeds — maar misschien wil je dat de kopieën ergens anders naartoe verwijzen.`,
    externalReferencesTruncated: (shown: number) => `Alleen de eerste ${shown} worden getoond.`,

    columnPage: "Pagina",
    columnProperty: "Eigenschap",
    columnStillLinksTo: "Verwijst nog naar",
  },
};

export default nl satisfies UmbLocalizationDictionary;
