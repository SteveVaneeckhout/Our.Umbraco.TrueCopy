/**
 * Language Alias: en
 * Language Int Name: English
 * Language Local Name: English
 * Language Culture: en
 *
 * The source of truth for every user-facing string in this package, and the per-key fallback for
 * every other language: culture `en` is Umbraco's `UMB_DEFAULT_LOCALIZATION_CULTURE`, so the
 * registry loads it whatever the backoffice is set to. Never translate this file - copy it.
 *
 * The `trueCopy` area prefix keeps these keys out of the way of core's, which share one flat
 * `area_key` namespace with ours.
 */
import type { UmbLocalizationDictionary } from "@umbraco-cms/backoffice/localization-api";

const en = {
  trueCopy: {
    // Entity action, as it reads in the document's context menu.
    actionLabel: "True Copy…",

    resultHeadline: (name: string) => (name ? `Copied ${name}` : "Copied"),
    done: "Done",

    // The number itself is rendered beside these, so the label carries only the noun.
    pagesCopied: (count: number) => (count === 1 ? "page copied" : "pages copied"),
    linksRepointed: (count: number) =>
      count === 1 ? "link repointed at the copies" : "links repointed at the copies",
    copiedPagesChanged: (count: number) =>
      count === 1 ? "copied page changed" : "copied pages changed",

    noExternalReferences: "Nothing in the copies still points at the original pages.",
    externalReferences: (count: number) =>
      count === 1
        ? "1 link points at a page that was not part of the copy. It is left as it is — it is still correct — but you may want the copies to link somewhere else."
        : `${count} links point at pages that were not part of the copy. Those are left as they are — they are still correct — but you may want the copies to link somewhere else.`,
    externalReferencesTruncated: (shown: number) => `Only the first ${shown} are listed.`,

    columnPage: "Page",
    columnProperty: "Property",
    columnStillLinksTo: "Still links to",
  },
} as const;

export default en satisfies UmbLocalizationDictionary;

/** The shape every other language file must match. See `types.ts`. */
export type TrueCopyLocalizations = typeof en;
