import { Component, ElementRef, forwardRef, viewChild } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * A small rich-text box: bold, italic, headings, bullet and numbered lists, links.
 *
 * Built on contenteditable rather than pulling in an editor package. The toolbar covers
 * exactly what the buying guide needs — the reference layout is headings, paragraphs, bullets
 * and one numbered list — and nothing more, which keeps the output inside the tag allowlist
 * the API sanitises against. Anything typed or pasted beyond it is stripped server-side, so
 * the worst case is markup quietly not surviving, never a broken page.
 *
 * document.execCommand is deprecated but is still the only thing every browser implements for
 * this, and the alternative is a selection-and-range editor of its own. Worth revisiting only
 * if a browser actually drops it.
 */
@Component({
  selector: 'app-rich-text',
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => RichTextComponent), multi: true }],
  template: `
    <div class="border border-slate-300 rounded-lg overflow-hidden bg-white">
      <div class="flex flex-wrap items-center gap-0.5 px-1.5 py-1 border-b border-slate-200 bg-slate-50">
        @for (b of buttons; track b.cmd + b.arg) {
          <button type="button" (mousedown)="$event.preventDefault()" (click)="run(b.cmd, b.arg)"
                  [title]="b.title"
                  class="px-2 py-1 text-sm rounded hover:bg-slate-200 text-slate-700 min-w-8">
            <span [class]="b.class">{{ b.label }}</span>
          </button>
        }
        <span class="w-px h-5 bg-slate-300 mx-1"></span>
        <button type="button" (mousedown)="$event.preventDefault()" (click)="addLink()"
                title="Add a link" class="px-2 py-1 text-sm rounded hover:bg-slate-200 text-slate-700">🔗</button>
        <button type="button" (mousedown)="$event.preventDefault()" (click)="run('unlink')"
                title="Remove link" class="px-2 py-1 text-sm rounded hover:bg-slate-200 text-slate-400">⛓️‍💥</button>
      </div>

      <div #editor contenteditable="true" (input)="onInput()" (blur)="onTouched()"
           class="px-3 py-2 min-h-40 max-h-96 overflow-y-auto text-sm text-slate-700 focus:outline-none editor-body"></div>
    </div>
    <p class="text-[11px] text-slate-400 mt-1">
      Formatting beyond these buttons is removed when saved.
    </p>
  `,
  styles: [`
    .editor-body :is(ul, ol) { padding-inline-start: 1.4rem; margin-block: 0.4rem; }
    .editor-body ul { list-style: disc; }
    .editor-body ol { list-style: decimal; }
    .editor-body :is(h2, h3) { font-weight: 700; margin-block: 0.5rem 0.25rem; }
    .editor-body h2 { font-size: 1.15rem; }
    .editor-body h3 { font-size: 1.05rem; }
    .editor-body p { margin-block: 0.4rem; }
    .editor-body a { color: #2563eb; text-decoration: underline; }
  `],
})
export class RichTextComponent implements ControlValueAccessor {
  private readonly editor = viewChild.required<ElementRef<HTMLDivElement>>('editor');

  readonly buttons = [
    { cmd: 'bold', arg: '', label: 'B', title: 'Bold', class: 'font-bold' },
    { cmd: 'italic', arg: '', label: 'I', title: 'Italic', class: 'italic' },
    { cmd: 'formatBlock', arg: 'h3', label: 'H', title: 'Heading', class: 'font-bold' },
    { cmd: 'formatBlock', arg: 'p', label: '¶', title: 'Normal text', class: '' },
    { cmd: 'insertUnorderedList', arg: '', label: '• —', title: 'Bullet list', class: 'text-xs' },
    { cmd: 'insertOrderedList', arg: '', label: '1. —', title: 'Numbered list', class: 'text-xs' },
  ];

  private value = '';
  private onChange: (v: string) => void = () => {};
  onTouched: () => void = () => {};

  writeValue(v: string | null): void {
    this.value = v ?? '';
    const el = this.editor()?.nativeElement;
    // Only written when it differs, so putting the cursor mid-sentence and typing does not
    // reset the caret to the start on every keystroke.
    if (el && el.innerHTML !== this.value) el.innerHTML = this.value;
  }

  registerOnChange(fn: (v: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }

  onInput(): void {
    this.value = this.editor().nativeElement.innerHTML;
    this.onChange(this.value);
  }

  run(cmd: string, arg = ''): void {
    this.editor().nativeElement.focus();
    document.execCommand(cmd, false, arg || undefined);
    this.onInput();
  }

  addLink(): void {
    const url = prompt('Link address (https://… or mailto:…)');
    if (!url?.trim()) return;
    this.run('createLink', url.trim());
  }
}
