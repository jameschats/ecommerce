/** A typed block of page content. See migration 055 for what Content holds per type. */
export interface ContentSection {
  sectionId: number;
  /** Prose | Faq | Stats | Cards | Cta */
  sectionType: string;
  /** Heading for Prose and Cards; the question for Faq. */
  title: string | null;
  /** Sanitised HTML for Prose and Faq; a JSON payload for Stats, Cards and Cta. */
  content: string | null;
  displayOrder: number;
  isVisible: boolean;
}

export interface ContentPage {
  pageId: number;
  title: string;
  slug: string;
  isPublished: boolean;
  metaTitle: string | null;
  metaDescription: string | null;
  sections: ContentSection[];
}
