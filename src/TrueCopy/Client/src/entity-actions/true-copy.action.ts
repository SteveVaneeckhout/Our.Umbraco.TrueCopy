import { postCopy } from "../api/index.js";
import { TRUE_COPY_RESULT_MODAL } from "./result-modal.token.js";
import { umbOpenModal } from "@umbraco-cms/backoffice/modal";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { UMB_ACTION_EVENT_CONTEXT } from "@umbraco-cms/backoffice/action";
import {
  UmbEntityActionBase,
  UmbRequestReloadChildrenOfEntityEvent,
} from "@umbraco-cms/backoffice/entity-action";
import {
  UMB_DOCUMENT_ENTITY_TYPE,
  UMB_DOCUMENT_ROOT_ENTITY_TYPE,
  UMB_DUPLICATE_DOCUMENT_MODAL,
  UmbDocumentItemRepository,
  UmbDocumentTreeRepository,
} from "@umbraco-cms/backoffice/document";
import {
  UmbDocumentTypeDetailRepository,
  UmbDocumentTypeStructureRepository,
} from "@umbraco-cms/backoffice/document-type";
import { linkEntityExpansionEntries } from "@umbraco-cms/backoffice/utils";
import type { UmbDocumentTreeItemModel } from "@umbraco-cms/backoffice/document";

/**
 * "True Copy…" in a document's tree menu.
 *
 * The destination picker is core's own duplicate modal (`UMB_DUPLICATE_DOCUMENT_MODAL`), so this action
 * looks and behaves exactly like the built-in Copy right up to the point where it repoints the links.
 * It also means the allowed-parent filtering and tree pre-expansion below are the same code paths core
 * uses, rather than a second implementation that could disagree with it.
 *
 * Note this modal needs no `UmbModalRouteRegistrationController`: it renders `umb-tree`, not
 * `umb-collection`, so nothing inside it fires the `navigationsuccess` that makes the modal manager
 * force-close router-less modals.
 */
export class TrueCopyEntityAction extends UmbEntityActionBase<never> {
  override async execute() {
    if (!this.args.unique) throw new Error("Unique is not available");
    if (!this.args.entityType) throw new Error("Entity Type is not available");

    const [selectableFilter, ancestors] = await Promise.all([
      this.#getSelectableFilter(this.args.unique),
      this.#requestAncestors(this.args.unique),
    ]);

    const value = await umbOpenModal(this, UMB_DUPLICATE_DOCUMENT_MODAL, {
      data: {
        unique: this.args.unique,
        entityType: this.args.entityType,
        selectableFilter,
        treeExpansion: ancestors.length ? linkEntityExpansionEntries(ancestors) : undefined,
      },
    }).catch(() => undefined);

    // Cancelled.
    if (!value) return;

    const destinationUnique = value.destination.unique;
    if (destinationUnique === undefined) throw new Error("Destination Unique is not available");

    const { data } = await tryExecute(
      this,
      postCopy({
        body: {
          sourceId: this.args.unique,
          targetParentId: destinationUnique,
          includeDescendants: value.includeDescendants,
          relateToOriginal: value.relateToOriginal,
        },
      }),
      { disableNotifications: false },
    );

    // tryExecute has already surfaced the failure as a notification.
    if (!data) return;

    await this.#reloadMenu(destinationUnique);

    await umbOpenModal(this, TRUE_COPY_RESULT_MODAL, { data: { result: data } }).catch(() => undefined);
  }

  /** Only offer parents the source's document type is actually allowed under. */
  async #getSelectableFilter(documentUnique: string) {
    const itemRepository = new UmbDocumentItemRepository(this);
    const { data } = await itemRepository.requestItems([documentUnique]);
    const item = data?.[0];
    if (!item) throw new Error("Item is not available");

    const documentTypeUnique = item.documentType.unique;
    const structureRepository = new UmbDocumentTypeStructureRepository(this);
    const typeDetailRepository = new UmbDocumentTypeDetailRepository(this);

    const [{ data: allowedParents }, { data: documentType }] = await Promise.all([
      structureRepository.requestAllowedParentsOf(documentTypeUnique),
      typeDetailRepository.requestByUnique(documentTypeUnique),
    ]);

    const isAllowedAtRoot = documentType?.allowedAtRoot ?? false;

    if (!allowedParents) return undefined;

    return (treeItem: UmbDocumentTreeItemModel) => {
      if (treeItem.unique === null) return isAllowedAtRoot;
      return allowedParents.some((parent) => parent.unique === treeItem.documentType?.unique);
    };
  }

  /** Pre-expand the destination tree down to the source, so the picker opens somewhere useful. */
  async #requestAncestors(unique: string) {
    try {
      const treeRepository = new UmbDocumentTreeRepository(this);
      const { data } = await treeRepository.requestTreeItemAncestors({
        treeItem: { unique, entityType: this.args.entityType },
      });
      // The API includes the node itself; only its parents want expanding.
      return data?.filter((item) => item.unique !== unique) ?? [];
    } catch {
      // Pre-expansion is a convenience. If it fails the picker still opens.
      return [];
    }
  }

  async #reloadMenu(destinationUnique: string | null) {
    const actionEventContext = await this.getContext(UMB_ACTION_EVENT_CONTEXT);
    if (!actionEventContext) return;

    actionEventContext.dispatchEvent(
      new UmbRequestReloadChildrenOfEntityEvent({
        unique: destinationUnique,
        entityType: destinationUnique === null ? UMB_DOCUMENT_ROOT_ENTITY_TYPE : UMB_DOCUMENT_ENTITY_TYPE,
      }),
    );
  }
}

export { TrueCopyEntityAction as api };
