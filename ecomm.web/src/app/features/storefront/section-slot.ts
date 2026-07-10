import { BuilderSection } from '../../core/services/cms.service';
import { ThemeSection } from '../../core/services/theme.service';

/** One entry in a section-composed page: a section type + (for authored sections) its data. */
export interface SectionSlot { type: string; data: BuilderSection | null; }

/** Map a published-theme section to the generic renderer's input shape. */
export function toBuilderSection(s: ThemeSection): BuilderSection {
  return {
    pageSectionId: s.id, pageId: 0, sectionType: s.sectionType, title: s.title,
    settings: s.settings, blocks: s.blocks, displayOrder: s.displayOrder, isVisible: s.isVisible,
  };
}

/** Build the slot list from a template's sections, or a built-in default order when none is authored. */
export function slotsFrom(sections: ThemeSection[], defaults: string[]): SectionSlot[] {
  return sections.length
    ? sections.map((s) => ({ type: s.sectionType, data: toBuilderSection(s) }))
    : defaults.map((t) => ({ type: t, data: null }));
}
