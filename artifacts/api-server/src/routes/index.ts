import { Router, type IRouter } from "express";
import healthRouter from "./health.js";
import stockRouter from "./stock.js";
import aiChatRouter from "./ai-chat.js";

const router: IRouter = Router();

router.use(healthRouter);
router.use(stockRouter);
router.use(aiChatRouter);

export default router;
