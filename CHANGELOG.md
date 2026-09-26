# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [17.0.0] - 2026-09-26

The Umbraco 17 LTS line, built from the `v17/main` branch. Same features as 18.0.0, pinned to
`[17.7.0,18.0.0)`.

### Changed

- The backoffice API's OpenAPI document is generated with Swashbuckle, as Umbraco 17 does, and is
  served at `/umbraco/swagger/truecopy/swagger.json`.

## [18.0.0] - 2026-09-26

Versioning now follows the Umbraco major: 18.x targets Umbraco 18, and a 17.x line on the
`v17/main` branch targets the Umbraco 17 LTS. No functional change from 1.0.0.

## [1.0.0] - 2026-09-19

First release. Targets Umbraco 18 deliberately, ahead of the v21 LTS, for teams happy to be early.

### Added

- True Copy… tree action that copies a subtree and repoints internal links at the copies.
- Rewriters for Content Picker, Multinode Treepicker, Multi URL Picker, Rich Text and Block List/Grid/Single Block.
- Reports every link still pointing outside the copy.
- ILinkRewriter extension point for custom property editors.
- English and Dutch translations.
