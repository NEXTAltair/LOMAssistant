"""把 README.md 转成发布压缩包里的「使用说明.txt」（纯文本，记事本打开好读）。

内容和 README 一致，只做格式转换：去掉图片和「开发」一节（玩家用不到），
标题加下划线，加粗、行内代码去掉标记，链接写成「文字（网址）」，表格转成两列对齐的行。
用法：python tools/readme-to-txt.py README.md 输出路径
"""
import re
import sys
import unicodedata

SKIP_SECTIONS = {'开发'}


def width(text):
    return sum(2 if unicodedata.east_asian_width(c) in 'WF' else 1 for c in text)


def inline(text):
    text = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', lambda m: m.group(1) if m.group(1) == m.group(2) else f'{m.group(1)}（{m.group(2)}）', text)
    text = re.sub(r'\*\*(.+?)\*\*', r'\1', text)
    text = re.sub(r'`([^`]+)`', r'\1', text)
    return text


def convert(lines):
    out, skipping, table = [], False, []

    def flush_table():
        rows = [r for r in table[2:]]  # 去掉表头和分隔行
        if rows:
            left = max(width(r[0]) for r in rows)
            for r in rows:
                out.append('  ' + r[0] + ' ' * (left - width(r[0]) + 2) + '　'.join(r[1:]))
        table.clear()

    for raw in lines:
        line = raw.rstrip()
        heading = re.match(r'^(#{1,3}) (.+)$', line)
        if heading:
            if table:
                flush_table()
            level, title = len(heading.group(1)), inline(heading.group(2))
            skipping = level == 2 and title in SKIP_SECTIONS
            if skipping:
                continue
            if level == 1:
                out += [title, '=' * width(title)]
            elif level == 2:
                out += ['\x00', title, '-' * width(title)]  # \x00：二级标题前空两行
            else:
                out += ['', '【' + title + '】']
            continue
        if skipping:
            continue
        stripped = line.strip()
        # 一排徽章（[![名字](图片)](链接)）：每个写成「名字：链接」一行
        badges = re.findall(r'\[!\[([^\]]*)\]\([^)]*\)\]\(([^)]+)\)', stripped)
        if badges and stripped.startswith('[!['):
            # 下载次数这类只是展示用的徽章，说明文件里不写
            out += [f'- {name}：{link}' for name, link in badges if name != '下载次数']
            continue
        if stripped.startswith('![') or stripped.startswith('<img'):
            continue
        if stripped.startswith('|'):
            table.append([inline(c.strip()) for c in stripped.strip('|').split('|')])
            continue
        if table:
            flush_table()
        if stripped.startswith('>'):
            indent = line[:len(line) - len(line.lstrip())]
            out.append(indent + inline(stripped.lstrip('> ').replace('⚠️ ', '注意：')))
            continue
        # 常见问题「**问？** 答」拆成两行
        qa = re.match(r'^\*\*(.+？)\*\* (.+)$', line)
        if qa:
            out += ['问：' + inline(qa.group(1)), '答：' + inline(qa.group(2))]
            continue
        text = inline(line)
        # 顶上一行「下载 · 网盘 · 视频 · 群」拆成一行一个
        if '　·　' in text:
            out += ['- ' + part.strip().replace('⬇ ', '').replace('▶ ', '') for part in text.split('　·　')]
            continue
        out.append(text)
    if table:
        flush_table()

    # 连续空行只留一行；二级标题前空两行
    result = []
    for line in out:
        if line == '\x00':
            while result and not result[-1].strip():
                result.pop()
            result += ['', '']
        elif line.strip() or (result and result[-1].strip()):
            result.append(line)
    return '\n'.join(result).strip() + '\n'


if __name__ == '__main__':
    src, dst = sys.argv[1], sys.argv[2]
    text = convert(open(src, encoding='utf-8').read().splitlines())
    with open(dst, 'w', encoding='utf-8-sig', newline='\r\n') as f:  # 带 BOM、CRLF：老记事本也不乱码
        f.write(text)
    print('written', dst)
