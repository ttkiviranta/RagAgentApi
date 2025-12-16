-- Fix MessageCount for existing conversations
-- Run this script to update MessageCount based on actual message counts

UPDATE conversations
SET message_count = (
    SELECT COUNT(*)
    FROM messages
    WHERE messages.conversation_id = conversations.id
)
WHERE message_count = 0 OR message_count IS NULL;

-- Verify results
SELECT 
    c.id,
    c.title,
    c.message_count as stored_count,
    COUNT(m.id) as actual_count
FROM conversations c
LEFT JOIN messages m ON m.conversation_id = c.id
GROUP BY c.id, c.title, c.message_count
ORDER BY c.last_message_at DESC;
