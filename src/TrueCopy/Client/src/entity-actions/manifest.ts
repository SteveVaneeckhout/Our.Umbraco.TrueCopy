import { TRUE_COPY_RESULT_MODAL_ALIAS } from "./result-modal.token.js";
import { UMB_DOCUMENT_ENTITY_TYPE, UMB_USER_PERMISSION_DOCUMENT_DUPLICATE } from "@umbraco-cms/backoffice/document";
import { UMB_ENTITY_IS_NOT_TRASHED_CONDITION_ALIAS } from "@umbraco-cms/backoffice/recycle-bin";

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: "entityAction",
    kind: "default",
    alias: "TrueCopy.EntityAction.Document.TrueCopy",
    name: "True Copy Document Entity Action",
    forEntityTypes: [UMB_DOCUMENT_ENTITY_TYPE],
    // Just below core's own Duplicate, so the two sit together and the familiar one stays first.
    weight: 599,
    api: () => import("./true-copy.action.js"),
    meta: {
      icon: "icon-documents",
      // Core runs a manifest label through `localize.string()`, so a `#`-prefixed key resolves the
      // same way it would inside an element.
      label: "#trueCopy_actionLabel",
    },
    // The same permissions core's Duplicate requires. The API re-checks both server-side, against the
    // same two content permissions, so the condition here is only about what the menu offers.
    conditions: [
      {
        alias: "Umb.Condition.UserPermission.Document",
        allOf: [UMB_USER_PERMISSION_DOCUMENT_DUPLICATE],
      },
      {
        alias: UMB_ENTITY_IS_NOT_TRASHED_CONDITION_ALIAS,
      },
    ],
  },
  {
    type: "modal",
    alias: TRUE_COPY_RESULT_MODAL_ALIAS,
    name: "True Copy Result Modal",
    js: () => import("./result-modal.element.js"),
  },
];
