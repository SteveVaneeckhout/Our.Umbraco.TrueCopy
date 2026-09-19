import type { TrueCopyResultModel } from "../api/index.js";
import { UmbModalToken } from "@umbraco-cms/backoffice/modal";

export interface TrueCopyResultModalData {
  result: TrueCopyResultModel;
}

export const TRUE_COPY_RESULT_MODAL_ALIAS = "TrueCopy.Modal.Result";

export const TRUE_COPY_RESULT_MODAL = new UmbModalToken<TrueCopyResultModalData, never>(
  TRUE_COPY_RESULT_MODAL_ALIAS,
  {
    modal: {
      type: "dialog",
      size: "medium",
    },
  },
);
