#nullable enable
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NuVatis.Generators.Diagnostics;

/**
 * XML 요소의 라인/컬럼 정보를 Roslyn Location으로 변환한다.
 * SG에서 Diagnostic 보고 시 원본 XML 위치를 가리키기 위해 사용.
 *
 * @author 최진호
 * @date   2026-02-24
 */
public static class XmlLocationMapper {

    public static Location CreateLocation(string filePath, XElement element) {
        if (element is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()) {
            return CreateLocation(filePath, lineInfo.LineNumber, lineInfo.LinePosition);
        }
        return Location.None;
    }

    public static Location CreateLocation(string filePath, int lineNumber, int columnNumber) {
        var lineIndex   = lineNumber > 0 ? lineNumber - 1 : 0;
        var columnIndex = columnNumber > 0 ? columnNumber - 1 : 0;
        var start       = new LinePosition(lineIndex, columnIndex);
        var end         = new LinePosition(lineIndex, columnIndex);
        var lineSpan    = new LinePositionSpan(start, end);

        return Location.Create(filePath, new TextSpan(0, 0), lineSpan);
    }
}
