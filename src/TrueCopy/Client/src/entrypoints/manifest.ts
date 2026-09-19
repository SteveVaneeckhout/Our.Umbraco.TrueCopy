export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "True Copy Entrypoint",
    alias: "TrueCopy.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];
