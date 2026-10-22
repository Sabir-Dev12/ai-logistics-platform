export const knowledgeDocument=(overrides={})=>({id:"doc-1",name:"Guide.txt",type:"TXT",size:5,date:"2026-10-18T10:00:00Z",status:"indexed",chunkCount:2,modelId:"local-embedding",errorCode:null,...overrides});
export const knowledgePage=(items,totalCount=items.length,page=1,pageSize=5)=>({items,totalCount,page,pageSize});
export const reply=(value,status=200)=>({ok:status>=200&&status<300,status,text:async()=>JSON.stringify(value)});
