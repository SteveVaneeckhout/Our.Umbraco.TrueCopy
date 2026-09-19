import type { TrueCopyResultModalData } from "./result-modal.token.js";
import { css, customElement, html, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_EDIT_DOCUMENT_WORKSPACE_PATH_PATTERN } from "@umbraco-cms/backoffice/document";

/**
 * What the copy did. Shown once, straight after the copy, because that is the moment an editor can act
 * on it - which is also why none of this is persisted.
 */
@customElement("true-copy-result")
export class TrueCopyResultElement extends UmbModalBaseElement<TrueCopyResultModalData, never> {
  #workspaceHref(unique: string) {
    return UMB_EDIT_DOCUMENT_WORKSPACE_PATH_PATTERN.generateAbsolute({ unique });
  }

  #renderStats() {
    const result = this.data!.result;

    // The counts sit in their own span so they can be styled as numbers, which is why the localized
    // label carries only the noun and the plural rule, not the figure itself.
    return html`
      <div class="stats">
        <div class="stat">
          <span class="count">${this.localize.number(result.copiedCount)}</span>
          <span class="muted">${this.localize.term("trueCopy_pagesCopied", result.copiedCount)}</span>
        </div>
        <div class="stat">
          <span class="count">${this.localize.number(result.rewrittenLinkCount)}</span>
          <span class="muted"
            >${this.localize.term("trueCopy_linksRepointed", result.rewrittenLinkCount)}</span
          >
        </div>
        <div class="stat">
          <span class="count">${this.localize.number(result.documentsChangedCount)}</span>
          <span class="muted"
            >${this.localize.term("trueCopy_copiedPagesChanged", result.documentsChangedCount)}</span
          >
        </div>
      </div>
    `;
  }

  #renderExternalReferences() {
    const result = this.data!.result;

    if (!result.externalReferenceCount) {
      return html`<p class="muted">${this.localize.term("trueCopy_noExternalReferences")}</p>`;
    }

    return html`
      <p class="muted">
        ${this.localize.term("trueCopy_externalReferences", result.externalReferenceCount)}
        ${result.externalReferencesTruncated
          ? html`<strong
              >${this.localize.term(
                "trueCopy_externalReferencesTruncated",
                result.externalReferences.length,
              )}</strong
            >`
          : nothing}
      </p>

      <div class="table-container">
        <uui-scroll-container>
          <uui-table>
            <uui-table-head>
              <uui-table-head-cell>${this.localize.term("trueCopy_columnPage")}</uui-table-head-cell>
              <uui-table-head-cell
                >${this.localize.term("trueCopy_columnProperty")}</uui-table-head-cell
              >
              <uui-table-head-cell
                >${this.localize.term("trueCopy_columnStillLinksTo")}</uui-table-head-cell
              >
            </uui-table-head>
            ${result.externalReferences.map(
              (reference) => html`
                <uui-table-row>
                  <uui-table-cell>
                    <a href=${this.#workspaceHref(reference.documentId)}>
                      ${reference.documentName ?? reference.documentId}
                    </a>
                  </uui-table-cell>
                  <uui-table-cell><span class="path">${reference.propertyAlias}</span></uui-table-cell>
                  <uui-table-cell>
                    <a href=${this.#workspaceHref(reference.targetId)}>
                      ${reference.targetName ?? reference.targetId}
                    </a>
                  </uui-table-cell>
                </uui-table-row>
              `,
            )}
          </uui-table>
        </uui-scroll-container>
      </div>
    `;
  }

  override render() {
    if (!this.data) return nothing;

    return html`
      <umb-body-layout
        headline=${this.localize.term("trueCopy_resultHeadline", this.data.result.rootCopyName ?? "")}>
        <uui-box> ${this.#renderStats()} ${this.#renderExternalReferences()} </uui-box>

        <uui-button
          slot="actions"
          look="primary"
          color="positive"
          label=${this.localize.term("trueCopy_done")}
          @click=${this._submitModal}></uui-button>
      </umb-body-layout>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      .stats {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-layout-1);
        margin-bottom: var(--uui-size-space-5);
      }
      .stat {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-1);
      }
      .count {
        font-size: var(--uui-type-h4-size);
        font-variant-numeric: tabular-nums;
      }
      .muted {
        color: var(--uui-color-text-alt);
      }
      .path {
        font-family: var(--uui-font-monospace);
      }
      .table-container {
        display: flex;
        align-items: flex-start;
      }
      .table-container uui-scroll-container {
        flex: 1;
        max-width: 100%;
        overflow-x: auto;
      }
    `,
  ];
}

export default TrueCopyResultElement;

declare global {
  interface HTMLElementTagNameMap {
    "true-copy-result": TrueCopyResultElement;
  }
}
