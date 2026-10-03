import { UserPlus, Users } from "lucide-react";
import { useState } from "react";
import { Button } from "../../components/ui/button";
import { DataTable, type TableColumn } from "../../components/ui/table";
import { formatNumber } from "../../lib/format";
import { guideMessages } from "./guideMessages";
import { GuideSection } from "./GuideSection";
import { sampleTeachers, type SampleTeacher } from "./sampleData";

const columns: readonly TableColumn<SampleTeacher>[] = [
  { key: "name", header: guideMessages.columnTeacher, cell: (row) => row.name },
  { key: "subject", header: guideMessages.columnSubject, cell: (row) => row.subject },
  { key: "lessons", header: guideMessages.columnLessons, cell: (row) => formatNumber(row.lessons), numeric: true },
];

export function TableSection() {
  const [selected, setSelected] = useState<string | null>("2");
  return (
    <GuideSection id="guide-table" title={guideMessages.table}>
      <p className="type-caption">{`${guideMessages.stateHover} · ${guideMessages.stateSelected}`}</p>
      <DataTable
        caption={guideMessages.table}
        columns={columns}
        rows={sampleTeachers}
        rowKey={(row) => row.id}
        selectedKey={selected}
        onSelect={(row) => setSelected(row.id)}
        demoHoverKey="3"
      />
      <p className="type-caption">{guideMessages.stateEmpty}</p>
      <DataTable
        caption={guideMessages.stateEmpty}
        columns={columns}
        rows={[]}
        rowKey={(row) => row.id}
        empty={(
          <div className="ui-empty-state">
            <Users aria-hidden="true" size={24} strokeWidth={2} />
            <p>{guideMessages.tableEmpty}</p>
            <Button icon={<UserPlus aria-hidden="true" size={20} />}>{guideMessages.addTeacher}</Button>
          </div>
        )}
      />
      <p className="type-caption">{guideMessages.stateLoading}</p>
      <DataTable caption={guideMessages.stateLoading} columns={columns} rows={[]} rowKey={(row) => row.id} loading />
    </GuideSection>
  );
}
